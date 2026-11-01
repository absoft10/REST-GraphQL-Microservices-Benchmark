# REST API: `Lms.Edge.Rest`

| | |
|---|---|
| Base URL | `http://{edge-rest-host}:{port}`. Plain HTTP/1.1 inside the VPC (DECISION-006, O-6). |
| Style | ASP.NET Core `[ApiController]` controllers (O-13) |
| Shared rules | [errors-and-pagination.md](errors-and-pagination.md) |
| Representations | **Full resource representations** (DECISION-002/AP-7, DECISION-004/O-4) |

## 1. Resource representations

### 1.1 Book ⟨P⟩: every column of the verified source dataset

Provisional field list (the manuscript-described attributes plus likely dataset columns). It is finalised after O-7:

| Field | JSON type | Nullable | ⟨P⟩ |
|---|---|---|---|
| `bookId` | integer | no | assigned in file order |
| `title` | string | no | ⟨P⟩ |
| `authors` | string | no | ⟨P⟩ |
| `description` | string | yes | ⟨P⟩, if present in the source |
| `category` | string | yes | ⟨P⟩ |
| `publisher` | string | yes | ⟨P⟩, if present |
| `price` | number (2 decimals as stored) | yes | ⟨P⟩ |
| `publishMonth` | string | yes | ⟨P⟩, if present |
| `publishYear` | integer | yes | ⟨P⟩ |

### 1.2 Member (approved: baseline `User` fields)

| Field | JSON type | Nullable |
|---|---|---|
| `memberId` | integer | no |
| `firstName` | string | no |
| `lastName` | string | no |
| `email` | string | no |
| `phoneNumber` | string | no |
| `address` | string | no |

### 1.3 Loan (approved: baseline `Loan` fields)

| Field | JSON type | Nullable |
|---|---|---|
| `loanId` | integer | no |
| `memberId` | integer | no |
| `bookId` | integer | no |
| `loanDate` | string `YYYY-MM-DD` | no |
| `returnDate` | string `YYYY-MM-DD` | **yes** (null = not returned) |
| `isReturned` | boolean | no |

Properties are serialised in the order listed. Order does not affect byte counts, but it makes bodies deterministic.

---

## 2. Endpoints

All endpoints are `GET`, take no request body, and return `200 application/json` on success.

### 2.1 List books (F-R)

```
GET /api/books?offset={offset}&limit={limit}
```

| Parameter | Type | Required | Rule |
|---|---|---|---|
| `offset` | int ≥ 0 | yes | errors-and-pagination §2 |
| `limit` | int 1–1000 | yes | errors-and-pagination §2 |

Exactly one of `offset`/`limit` **or** `ids` (§2.2) must be supplied. Supplying both, or neither, returns 400 `…/invalid-parameter`.

**Response:** a JSON array of Book (§1.1), ordered by `bookId` ascending.

Example. The values are placeholders, and `limit=2` is used for brevity:
```http
GET /api/books?offset=0&limit=2 HTTP/1.1
Host: edge-rest:8080
Accept: application/json
Accept-Encoding: identity
```
```json
[{"bookId":1,"title":"T1","authors":"A1","description":"D1","category":"C1","publisher":"P1","price":12.50,"publishMonth":"M1","publishYear":2004},{"bookId":2,"title":"T2","authors":"A2","description":null,"category":null,"publisher":"P2","price":null,"publishMonth":null,"publishYear":1998}]
```

### 2.2 Get books by IDs (N-RM call 3)

```
GET /api/books?ids={id,id,...}
```

`ids` follows the rules in errors-and-pagination §3. **Response:** a JSON array of Book, ordered by `bookId` ascending. Unknown IDs are omitted.

### 2.3 List members (N-RM call 1)

```
GET /api/members?offset={offset}&limit={limit}
```

**Response:** a JSON array of Member (§1.2), ordered by `memberId` ascending.

```json
[{"memberId":1,"firstName":"F1","lastName":"L1","email":"e1@example.invalid","phoneNumber":"P1","address":"ADDR1"}]
```

### 2.4 Get loans by member IDs (N-RM call 2)

```
GET /api/loans?memberIds={id,id,...}
```

**Response:** a JSON array of Loan (§1.3), ordered by (`memberId`, `loanId`) ascending. Members with no loans contribute nothing.

```json
[{"loanId":101,"memberId":1,"bookId":17,"loanDate":"2026-03-02","returnDate":"2026-03-20","isReturned":true},{"loanId":102,"memberId":1,"bookId":42,"loanDate":"2026-05-11","returnDate":null,"isReturned":false}]
```

### 2.5 Members with loans and books: the composite (N-RC)

```
GET /api/members-with-loans?offset={offset}&limit={limit}
```

**Response:** a JSON array of Member objects, each with an added `loans` array. Each loan has an added `book` object. Every embedded resource is a **full representation** (AP-7, AD-10):

```json
[{"memberId":1,"firstName":"F1","lastName":"L1","email":"e1@example.invalid","phoneNumber":"P1","address":"ADDR1","loans":[{"loanId":101,"memberId":1,"bookId":17,"loanDate":"2026-03-02","returnDate":"2026-03-20","isReturned":true,"book":{"bookId":17,"title":"T17","authors":"A17","description":null,"category":"C17","publisher":"P17","price":9.99,"publishMonth":"M17","publishYear":2011}}]}]
```

**Server-side aggregation (B-8).** It uses exactly 3 internal calls, in this order. They are the same calls and parameters as N-G and N-RM ([internal-domain-api.md](internal-domain-api.md)):
1. `GET /internal/members?offset&limit` (Members)
2. `GET /internal/loans?memberIds=<sorted unique>` (Loans)
3. `GET /internal/books?ids=<sorted unique>` (Catalog)

Aggregation is an in-memory join in `Lms.Edge.Rest`. The implementing file and function are recorded in B-8 at implementation.

### 2.6 Health (not part of any workload)

```
GET /health  →  200 text/plain "Healthy"
```

---

## 3. Errors

The errors are as specified in [errors-and-pagination.md](errors-and-pagination.md) §4:
- 400, for invalid parameters or ID lists;
- 404, for an unknown route;
- 502, for an upstream domain failure;
- 500, for an internal error.

All errors are `application/problem+json`.

## 4. Not provided (by design)

- Write operations: no `POST`, `PUT` or `DELETE` (D-5).
- HTTP caching headers (B-7).
- Compression (O-6).
- HATEOAS links and paging envelopes (README §3.1).
- Sparse fieldsets such as `?fields=` (full representations, AP-7).
