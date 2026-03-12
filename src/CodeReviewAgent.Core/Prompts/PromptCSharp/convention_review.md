# CODING CONVENTION RULES (.NET 10)

> Áp dụng cho tất cả project .NET. Project-specific rules sẽ override các rule này nếu có conflict.

---

## 1. Naming Convention

### 1.1 Bảng quy ước
| Element | Convention | Ví dụ đúng | Ví dụ sai |
|---------|-----------|------------|-----------|
| Class / Record | PascalCase | `UserService`, `OrderCreatedEvent` | `userService`, `order_service` |
| Interface | PascalCase + prefix `I` | `IRepository`, `ILogger` | `Repository`, `iLogger` |
| Method | PascalCase | `GetUser()`, `CalculateTotal()` | `getUser()`, `calculate_total()` |
| Variable / Local | camelCase hoặc snake_case | `userId`, `user_id` | `UserId`, `USERID` |
| Parameter | camelCase hoặc snake_case | `string name`, `string user_id` | `string Name` |
| Private Field | `_camelCase` hoặc snake_case | `_logger`, `_repository` | `logger`, `mLogger` |
| Constant | PascalCase | `DefaultTimeout`, `MaxRetries` | `DEFAULT_TIMEOUT`, `maxRetries` |
| Enum | PascalCase | `StatusType.Active` | `statusType.active` |
| Async Method | Suffix `Async` | `GetDataAsync()` | `GetData()` khi là async |
| Generic Type | `T` + tên đại diện | `TEntity`, `TRequest` | `T1`, `Type` |
| Bool variable/method | Prefix `Is`, `Can`, `Has` | `IsValid()`, `HasPermission` | `Valid()`, `CheckPermission` |

### 1.2 Các lỗi naming phổ biến cần flag
```
❌ enum ColorEnum { }        → ✅ enum Color { }
❌ class CDateTime { }       → ✅ class DateTime { }
❌ People.PeopleName         → ✅ People.Name
❌ bool CheckPrime(int n)    → ✅ bool IsPrime(int n)
❌ bool checked = true       → ✅ bool isChecked = true
```

---

## 2. Layout & Formatting

- **Indentation**: 4 spaces = 1 tab. Dùng tab cho block `{ }`, space cho điều kiện `if/while/for`.
- **Braces**: luôn đặt trên dòng mới (Allman style).
- **Line length**: ≤ 120 ký tự. Flag nếu vượt quá đáng kể (> 150 ký tự).
- **One class per file**: mỗi file `.cs` chứa đúng 1 class (ngoại trừ `record`, `enum` nhỏ).
- **File name**: phải trùng với tên class/interface/enum bên trong.

---

## 3. Classes & Methods

### Class
- Tên class = tên file, PascalCase.
- Dùng `sealed` cho class không có kế thừa.
- Class chỉ có static methods → khai báo `static class`.
- Namespace phải phản ánh folder structure.

### Method
- Tên method là **động từ** diễn đạt chức năng: `CalculateSalary()`, `ValidateUser()`.
- Độ dài ≤ 30 dòng. Flag nếu > 50 dòng.
- Số parameter ≤ 3. Nếu ≥ 4 → dùng record hoặc DTO.

```csharp
// ❌ SAI: quá nhiều parameter
public Order CreateOrder(string userId, string productId, int qty, decimal price, string note)

// ✅ ĐÚNG: dùng record/DTO
public Order CreateOrder(CreateOrderRequest request)
```

---

## 4. Properties & Fields

- Private field: prefix `_` + camelCase → `_logger`, `_connectionString`.
- Ưu tiên auto-property: `public string Name { get; set; }`
- Dùng `readonly` hoặc `init` cho field không thay đổi sau khởi tạo.
- **Không hard-code magic number/string** → dùng `const` hoặc `static readonly`.

```csharp
// ❌ SAI: magic number
if (retryCount > 3) { }

// ✅ ĐÚNG
private const int MaxRetries = 3;
if (retryCount > MaxRetries) { }
```

---

## 5. Error Handling

### Rules bắt buộc
- **Luôn kiểm tra nullable** cho variable, argument, function result.
- **Không dùng `catch (Exception ex) { }`** trống rỗng.
- **Không re-throw mất stack trace**:

```csharp
// ❌ SAI: mất stack trace
catch (Exception ex) { throw ex; }

// ✅ ĐÚNG: giữ stack trace
catch (Exception ex) { throw; }
```

- Tạo **custom exception** cho domain/business error:

```csharp
// ✅ ĐÚNG
public class OrderNotFoundException : Exception
{
    public OrderNotFoundException(int orderId)
        : base($"Order with id {orderId} not found.") { }
}
```

---

## 6. Async & Await

- Method async phải có suffix `Async`.
- **Luôn dùng `ConfigureAwait(false)`** trong library code, service layer, repository.
- **Tránh `async void`** (ngoại lệ duy nhất: event handler).
- Dùng `CancellationToken` cho API operation dài.

```csharp
// ❌ SAI
public async void SendEmail() { }

// ✅ ĐÚNG
public async Task SendEmailAsync(CancellationToken ct = default) { }
```

---

## 7. LINQ & Collections

- Ưu tiên **method syntax** (`.Select()`, `.Where()`) thay vì query syntax.
- Tránh lồng LINQ phức tạp → tách biến trung gian.
- Dùng `var` khi type rõ ràng từ vế phải.

```csharp
// ❌ SAI: LINQ lồng phức tạp
var result = items.Where(x => x.Orders.Any(o => o.Items.Sum(i => i.Price) > 100)).ToList();

// ✅ ĐÚNG: tách rõ ràng
var highValueOrders = items
    .Where(x => x.Orders.Any(HasHighValue))
    .ToList();
```

---

## 8. Dependency Injection & SOLID

- **Constructor injection** được ưu tiên. Không dùng Service Locator pattern.
- **Số dependency inject ≤ 4**. Nếu > 4 → flag để refactor.
- Không tạo interface cho mọi class — chỉ tạo khi cần abstraction thực sự.

### SOLID checklist nhanh
| Nguyên lý | Dấu hiệu vi phạm cần flag |
|-----------|--------------------------|
| SRP | Class vừa xử lý business logic vừa gọi DB vừa gửi email |
| OCP | Thêm loại mới bằng cách thêm `if/switch` vào class hiện có |
| LSP | Override method cha để throw `NotImplementedException` |
| ISP | Interface có method mà implementer để trống hoặc throw |
| DIP | Class cấp cao new trực tiếp class cụ thể thay vì inject interface |

---

## 9. Comments & Documentation

- **Không comment "cái gì"** → code phải tự giải thích.
- **Comment "tại sao"** khi logic không hiển nhiên.
- Dùng XML doc cho public API:

```csharp
/// <summary>
/// Calculate total price with tax.
/// </summary>
public decimal CalculateTotal(decimal amount, decimal taxRate) { ... }
```

---

## 10. Unit Test Convention

- Tên test: `MethodName_StateUnderTest_ExpectedBehavior`
- Theo AAA pattern: Arrange → Act → Assert.

```csharp
[Fact]
public void CalculateTotal_WithTax_ReturnsCorrectAmount()
{
    // Arrange
    var amount = 100m;
    var taxRate = 0.1m;

    // Act
    var result = _service.CalculateTotal(amount, taxRate);

    // Assert
    Assert.Equal(110m, result);
}
```

