# SECURITY REVIEW RULES

> Stack: .NET 10, PostgreSQL (EF Core), MongoDB, Redis

---

## 1. SQL Injection (EF Core + PostgreSQL)

```csharp
// ❌ CRITICAL: raw string interpolation trong query
var result = ctx.Users
    .FromSqlRaw($"SELECT * FROM users WHERE name = '{name}'")
    .ToList();

// ❌ CRITICAL: string.Format trong raw SQL
ctx.Database.ExecuteSqlRaw(string.Format("DELETE FROM orders WHERE id = {0}", id));

// ✅ ĐÚNG: parameterized
ctx.Users.FromSqlRaw("SELECT * FROM users WHERE name = {0}", name);
// hoặc dùng LINQ (an toàn nhất)
ctx.Users.Where(u => u.Name == name).ToList();
```

**Flag khi thấy**: `FromSqlRaw`, `ExecuteSqlRaw`, `ExecuteSqlInterpolated` kết hợp với string interpolation `$"..."` hoặc `+` concatenation.

---

## 2. NoSQL Injection (MongoDB)

```csharp
// ❌ CRITICAL: build filter từ raw string input
var filter = $"{{ name: '{userInput}' }}";

// ✅ ĐÚNG: dùng strongly-typed filter builder
var filter = Builders<User>.Filter.Eq(u => u.Name, userInput);
```

---

## 3. Secrets & Credentials

**Flag CRITICAL khi thấy bất kỳ pattern nào sau trong code**:
- Connection string hardcoded: `"Server=...;Password=..."`
- API key dạng string literal: `"sk-..."`, `"Bearer eyJ..."` 
- Password trong code: `password = "admin123"`
- Private key hoặc certificate inline

✅ Secrets phải lấy từ: `IConfiguration`, environment variable, hoặc secret manager.

---

## 4. Authentication & Authorization

```csharp
// ❌ HIGH: endpoint thiếu [Authorize]
[HttpDelete("{id}")]
public async Task<IActionResult> DeleteUser(int id) { }

// ❌ HIGH: bypass authorization bằng role string thô
if (user.Role == "admin") { }  // dễ typo, không type-safe

// ✅ ĐÚNG
[Authorize(Roles = nameof(UserRole.Admin))]
[HttpDelete("{id}")]
public async Task<IActionResult> DeleteUser(int id) { }
```

---

## 5. Input Validation

```csharp
// ❌ HIGH: không validate input trước khi xử lý
public async Task<IActionResult> CreateOrder(OrderRequest request)
{
    await _service.CreateAsync(request);  // không validate
}

// ✅ ĐÚNG: dùng FluentValidation hoặc DataAnnotations
[HttpPost]
public async Task<IActionResult> CreateOrder([FromBody] OrderRequest request)
{
    if (!ModelState.IsValid) return BadRequest(ModelState);
    await _service.CreateAsync(request);
}
```

---

## 6. Redis — Sensitive Data

```csharp
// ❌ HIGH: lưu sensitive data vào Redis không encrypt
await _cache.SetStringAsync("user:123", JsonSerializer.Serialize(userWithPassword));

// ✅ ĐÚNG: chỉ cache non-sensitive data, hoặc encrypt trước khi cache
await _cache.SetStringAsync("user:123", JsonSerializer.Serialize(userSafeDto));
```

---

## 7. Exception Information Leak

```csharp
// ❌ HIGH: expose exception detail ra response
catch (Exception ex)
{
    return BadRequest(ex.Message);      // có thể leak stack trace, connection string
    return BadRequest(ex.ToString());   // tệ hơn
}

// ✅ ĐÚNG: log internal, trả về message chung
catch (Exception ex)
{
    _logger.LogError(ex, "Error processing request");
    return StatusCode(500, "An error occurred");
}
```

---

## 8. Mass Assignment

```csharp
// ❌ HIGH: bind trực tiếp entity từ request (user có thể set IsAdmin = true)
public async Task UpdateUser([FromBody] User user)
{
    _ctx.Users.Update(user);
}

// ✅ ĐÚNG: dùng DTO, chỉ map field được phép
public async Task UpdateUser([FromBody] UpdateUserRequest request)
{
    var user = await _ctx.Users.FindAsync(request.Id);
    user.Name = request.Name;  // chỉ update field cho phép
}
```

