# AurSil AI Assistant (MCP Connector): How It Works

This guide explains the AI Assistant feature in simple words. With it, a user can type
**"buy 2 grams of gold"** in ChatGPT or Claude, and the order is created on their own
AurSil account and saved in the database.

---

## 1. The idea in one picture

```
 You type in ChatGPT / Claude:  "buy 2 grams of gold"
              │
              ▼
 ┌─────────────────────────┐
 │  ChatGPT / Claude (AI)  │  The AI understands what you want and picks a "tool":
 │                         │  buy_metal(metal="gold", grams=2)
 └───────────┬─────────────┘
             │  HTTPS call to your connector link
             │  https://<your-ngrok>.ngrok-free.dev/mcp/aurs_XXXXXXXX
             ▼
 ┌─────────────────────────┐
 │  ngrok                  │  Sends internet traffic to your PC (localhost:5168)
 └───────────┬─────────────┘
             ▼
 ┌─────────────────────────┐
 │  AurSil API (.NET)      │
 │  1. McpAuth             │  Reads the key "aurs_XXXX" from the link → finds the user
 │  2. AurSilTools         │  Runs buy_metal for that user
 │  3. TradeService        │  Same buy logic the phone app uses
 └───────────┬─────────────┘
             ▼
 ┌─────────────────────────┐
 │  SQL Server (aursil)    │  Order saved, stock reduced, activity logged
 └─────────────────────────┘
             │
             ▼
 The AI replies: "Order AS26... created. Pay ₹26,113.65 here: <payment link>"
```

**MCP (Model Context Protocol)** is a standard way for an AI app to call tools on an outside
server. The AI does not touch the database itself. It can only call the tools the AurSil API
offers, and only for the account the key belongs to.

---

## 2. What the user sees (UI)

### In the AurSil phone app → Profile tab

A row named **🤖 AI Assistant** sits below "Change Password".

| Row text | Meaning |
|---|---|
| "Buy and sell by chatting in ChatGPT or Claude" | Not connected yet |
| "Connected • tap to get a new link or disconnect" | A link exists and works |

When the user taps it, a menu opens:

| Option | What happens |
|---|---|
| **Create connector link** (or **Create a new link**) | The app asks the server for a new link, **copies it to the clipboard** and shows it once. Any old link stops working. |
| **How to connect** | Shows the steps for Claude and ChatGPT (see section 5). |
| **Disconnect** (red, only when connected) | Deletes the key. The link stops working at once. |

Files:
- `AurSil.Mobile/Views/ProfilePage.xaml`: the row
- `AurSil.Mobile/Views/ProfilePage.xaml.cs`: `OnAssistantTapped` and `RefreshAssistantAsync`
- `AurSil.Mobile/Services/ApiClient.cs`: `GetAssistantAsync`, `CreateAssistantLinkAsync`, `DisconnectAssistantAsync`

The Chrome preview (`preview/aursil.html`) has the same row in Profile.

### In ChatGPT / Claude

The user just chats normally:
- "What is the gold price today?"
- "Buy 2 grams of gold"
- "Buy gold for ₹5000"
- "Did I pay for my last order?"
- "Sell 0.5 g gold"
- "Show my portfolio"

---

## 3. How the link (key) works

A connector link looks like this:

```
https://legume-cardiac-shiny.ngrok-free.dev/mcp/aurs_k3J9x...Qw
                                            └─┬┘└────┬───────┘
                                        MCP path   personal key
```

- The key is `aurs_` + 24 random bytes. It is unique to one user.
- **The database stores only the SHA-256 hash of the key, never the key itself.**
  This works like a password: if someone reads the database, they still can't rebuild the link.
- Creating a new link replaces the hash, so the old link dies.
- Disconnecting sets the hash to NULL.
- Only users with status **Active** can use a link. A Blocked or Pending user gets 401.

> ⚠️ Treat the link like a password. Anyone who has it can trade on that account.

### Database (`Database/aursil_schema_v4.sql`)

Two new columns in `dbo.Users`:

| Column | Purpose |
|---|---|
| `McpKeyHash CHAR(64)` | SHA-256 hex of the key (NULL = not connected) |
| `McpKeyCreatedAt DATETIME2` | When the link was created |

A unique filtered index (`UX_Users_McpKeyHash`) makes the lookup fast and stops two users from
having the same key.

---

## 4. Backend pieces (API)

### 4a. REST endpoints the app uses (need normal login token)

In `AurSil.Api/Controllers/AccountController.cs`:

| Method | URL | Returns | What it does |
|---|---|---|---|
| GET | `/api/assistant` | `{ connected, createdAt }` | Is a link active? |
| POST | `/api/assistant/key` | `{ url }` | Make a new key, save its hash, return the full link once. Logs **AssistantConnected**. |
| DELETE | `/api/assistant/key` | message | Remove the key. Logs **AssistantDisconnected**. |

Key logic lives in `AurSil.Api/Services/McpKeyService.cs`:
- `CreateAsync`: make key, store hash
- `RevokeAsync`: clear hash
- `FindUserAsync(key)`: hash the incoming key, find the Active user
- `PublicBaseUrl`: the public address used in links. It comes from `Mcp:PublicBaseUrl` in
  `appsettings.Development.json` (your ngrok URL).

### 4b. The MCP endpoint the AI uses

In `AurSil.Api/Program.cs`:

```csharp
builder.Services.AddMcpServer(...).WithHttpTransport().WithTools<AurSilTools>();
...
app.UseMcpAuth();          // check the key first
app.MapMcp("/mcp/{key}");  // then the MCP server answers
```

**Step 1: `Mcp/McpAuth.cs` (the gatekeeper)**
Every request to `/mcp/...` goes through here first:
1. It takes the key from the URL.
2. It calls `McpKeyService.FindUserAsync(key)`.
3. If no user is found, it returns **401** with "This AurSil connector link is invalid or was disconnected...".
4. If a user is found, it saves that user in `HttpContext.Items["mcp_user"]` and lets the request through.

**Step 2: `Mcp/AurSilTools.cs` (the tools)**
These are the only things the AI can do:

| Tool | What it does | Changes data? |
|---|---|---|
| `get_prices` | Live buy/sell rate per gram, discount %, GST % | No |
| `get_portfolio` | Grams held, invested, current value, profit/loss | No |
| `buy_metal(metal, grams or amount_inr)` | Creates a buy order and returns a **payment_url** | Yes (order created) |
| `sell_metal(metal, grams)` | Sells right away and pays the saved bank account | Yes (cannot undo) |
| `check_order(order_no)` | Is the order paid and credited? | No |
| `list_transactions(limit)` | Last buys and sells | No |
| `get_offers` | Active offers and discounts | No |

Each tool has a `[Description]`. The AI reads these descriptions to decide which tool to call
and what to tell the user. For example, buy_metal's description says "always show them payment_url".

`Normalize()` turns "Gold", "24k gold" and similar inputs into `gold`.

**Step 3: `Services/TradeService.cs` (shared buy/sell logic)**
The phone app (`OrdersController`) and the AI tools both call the **same** `TradeService`,
so the rules are identical everywhere:
- the price and discount are the same
- stock is checked and reduced
- you can't sell more than you hold
- sells need a bank account

The only difference: when the AI does it, the activity log entry ends with **"(via AI assistant)"**,
so the admin can see where each order came from.

---

## 5. How to connect (user steps)

1. Start the API on your PC:
   ```powershell
   cd E:\AurSil\AurSil.Api
   $env:DOTNET_ROOT='E:\DevTools\dotnet'
   E:\DevTools\dotnet\dotnet.exe run --launch-profile http
   ```
2. Start ngrok so the internet can reach it: `ngrok http 5168`
   (its URL must match `Mcp:PublicBaseUrl` in `appsettings.Development.json`).
3. In the app, go to **Profile → AI Assistant → Create connector link**. The link is copied.
4. Add the link in the AI app:
   - **Claude:** Settings → Connectors → Add custom connector → paste the link.
   - **ChatGPT:** Settings → Apps & Connectors → Advanced → turn on Developer mode → Create →
     paste the link, authentication: **None**.
5. Chat: "buy 2 grams of gold".

---

## 6. Full example: "buy 2 grams of gold"

| # | Where | What happens |
|---|---|---|
| 1 | Chat | User: "buy 2 grams of gold" |
| 2 | AI | Confirms, then calls `buy_metal(metal="gold", grams=2)` |
| 3 | McpAuth | Key → user `suman01` (Active) ✅ |
| 4 | TradeService | Rate with 5% discount, + GST → order **Created**, stock reserved, activity "Buy 2 g gold (via AI assistant)" |
| 5 | AI reply | "Order AS26… — pay ₹26,113.65 at https://…/pay/AS26…" |
| 6 | User | Opens the link and pays (Razorpay test) |
| 7 | Payment | Order becomes **Completed** and 2 g gold is added to the portfolio |
| 8 | Chat | "Did it go through?" → `check_order` → "Paid and credited ✅" |
| 9 | Phone app | Home and Portfolio show the new 2 g |

**Why does buying need a payment link?** The AI can't take money. The user always pays
themselves, so the AI can never spend anyone's money by mistake.
**Selling** has no payment step: the money goes straight to the saved bank account. That's
why `sell_metal` is marked `Destructive = true`, and AI apps ask the user to confirm first.

---

## 7. Common problems

| Problem | Reason / Fix |
|---|---|
| AI says it can't reach the server | The API or ngrok is not running. Start both (section 5). |
| 401 "link is invalid or was disconnected" | A new link was created or you disconnected. Create a new link and update the connector. |
| 401 for a user who had a working link | The user is no longer **Active** (Blocked/Pending). |
| Payment link opens localhost | `Mcp:PublicBaseUrl` is empty or wrong. Set it to your ngrok URL. |
| ngrok URL changed | Update `Mcp:PublicBaseUrl`, restart the API, create a new link. |
| "Add a bank account" on sell | Add one in the app: Profile → Bank Account. |

---

## 8. Test the MCP endpoint in Postman (optional)

`POST http://localhost:5168/mcp/<your key>`

Headers:
```
Content-Type: application/json
Accept: application/json, text/event-stream
```

Body (list the tools):
```json
{ "jsonrpc": "2.0", "id": 1, "method": "tools/list" }
```

Body (get prices):
```json
{ "jsonrpc": "2.0", "id": 2, "method": "tools/call",
  "params": { "name": "get_prices", "arguments": {} } }
```

A wrong key returns **401**, which shows the gatekeeper is working.

---

## 9. File map

| Layer | File |
|---|---|
| DB | `Database/aursil_schema_v4.sql` |
| Key logic | `AurSil.Api/Services/McpKeyService.cs` |
| Gatekeeper | `AurSil.Api/Mcp/McpAuth.cs` |
| AI tools | `AurSil.Api/Mcp/AurSilTools.cs` |
| Shared trading | `AurSil.Api/Services/TradeService.cs` |
| App endpoints | `AurSil.Api/Controllers/AccountController.cs` |
| Setup | `AurSil.Api/Program.cs`, `appsettings*.json` |
| Phone UI | `AurSil.Mobile/Views/ProfilePage.xaml(.cs)`, `Services/ApiClient.cs`, `Services/Models.cs` |
| Web preview | `preview/aursil.html` |
