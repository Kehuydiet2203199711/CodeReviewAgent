# PERFORMANCE REVIEW RULES

> Stack: .NET 10, EF Core + PostgreSQL, MongoDB, Redis

---

## 1. EF Core — N+1 Query Problem

```csharp
// ❌ HIGH: N+1 — mỗi order trigger 1 query riêng lấy customer
var orders = await _ctx.Orders.ToListAsync();
foreach (var order in orders)
{
    Console.WriteLine(order.Customer.Name);  // lazy load = N queries
}

// ✅ ĐÚNG: eager load với Include
var orders = await _ctx.Orders
    .Include(o => o.Customer)
    .ToListAsync();
```

**Flag khi thấy**: access navigation property trong loop mà không có `.Include()` trong query.

---

## 2. EF Core — Select toàn bộ khi chỉ cần một phần

```csharp
// ❌ MEDIUM: load toàn bộ entity chỉ để lấy 1-2 field
var users = await _ctx.Users.ToListAsync();
var names = users.Select(u => u.Name).ToList();

// ✅ ĐÚNG: project trong query, không load toàn bộ
var names = await _ctx.Users
    .Select(u => u.Name)
    .ToListAsync();
```

---

## 3. EF Core — AsNoTracking

```csharp
// ❌ MEDIUM: query read-only nhưng vẫn tracking
var orders = await _ctx.Orders
    .Where(o => o.Status == OrderStatus.Pending)
    .ToListAsync();  // tracking không cần thiết nếu chỉ đọc

// ✅ ĐÚNG: thêm AsNoTracking cho read-only query
var orders = await _ctx.Orders
    .AsNoTracking()
    .Where(o => o.Status == OrderStatus.Pending)
    .ToListAsync();
```

---

## 4. EF Core — Gọi DB trong loop

```csharp
// ❌ CRITICAL: query DB trong loop
foreach (var id in orderIds)
{
    var order = await _ctx.Orders.FindAsync(id);  // N queries
    await ProcessAsync(order);
}

// ✅ ĐÚNG: batch query
var orders = await _ctx.Orders
    .Where(o => orderIds.Contains(o.Id))
    .ToListAsync();
foreach (var order in orders) { await ProcessAsync(order); }
```

---

## 5. Redis — Cache Miss Storm / Missing Expiry

```csharp
// ❌ HIGH: cache không có expiry → memory leak
await _cache.SetStringAsync("key", value);

// ❌ HIGH: không handle cache miss → stampede khi cache cold start
var cached = await _cache.GetStringAsync("key");
// nếu null thì sao? không có fallback

// ✅ ĐÚNG
var cached = await _cache.GetStringAsync(key);
if (cached != null) return JsonSerializer.Deserialize<T>(cached);

var data = await _db.GetDataAsync();
await _cache.SetStringAsync(key, JsonSerializer.Serialize(data),
    new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    });
return data;
```

---

## 6. Async — Blocking Call

```csharp
// ❌ CRITICAL: blocking async trên sync context → deadlock nguy cơ cao
var result = _service.GetDataAsync().Result;
var result = _service.GetDataAsync().GetAwaiter().GetResult();
Task.Run(() => _service.GetDataAsync()).Wait();

// ✅ ĐÚNG: await all the way
var result = await _service.GetDataAsync();
```

---

## 7. MongoDB — Missing Index / Full Collection Scan

```csharp
// ❌ HIGH: filter trên field không có index trong collection lớn
var users = await _collection
    .Find(u => u.Email == email)  // nếu Email không có index
    .FirstOrDefaultAsync();
```

**Flag khi thấy**: filter hoặc sort trên field mà không có evidence về index (không thấy `CreateIndex` hoặc index attribute). Hạ severity xuống MEDIUM nếu collection được biết là nhỏ.

---

## 8. String — Concatenation trong Loop

```csharp
// ❌ MEDIUM: string concat trong loop → O(n²) memory
string result = "";
foreach (var item in items)
{
    result += item.ToString();  // tạo object mới mỗi vòng
}

// ✅ ĐÚNG
var sb = new StringBuilder();
foreach (var item in items) sb.Append(item);
string result = sb.ToString();
```

---

## 9. LINQ — ToList() không cần thiết

```csharp
// ❌ MEDIUM: ToList() sớm, mất khả năng compose query
var users = _ctx.Users.ToList().Where(u => u.IsActive);  // load ALL users về memory trước

// ✅ ĐÚNG: compose query trước khi execute
var users = _ctx.Users.Where(u => u.IsActive).ToList();
```

---

## 10. Parallel / Concurrent — Shared State

```csharp
// ❌ HIGH: dùng List (non-thread-safe) trong Parallel.ForEach
var results = new List<Result>();
Parallel.ForEach(items, item =>
{
    results.Add(Process(item));  // race condition
});

// ✅ ĐÚNG: dùng ConcurrentBag hoặc PLINQ
var results = items.AsParallel().Select(Process).ToList();
```

