# PROJECT-SPECIFIC REVIEW RULES
# Project: [TÊN PROJECT]
# Service: [TÊN SERVICE]
# Last updated: [DATE]

> File này override hoặc bổ sung cho BASE_REVIEW.md, CONVENTION_REVIEW.md,
> SECURITY_REVIEW.md, PERFORMANCE_REVIEW.md.
> Chỉ cần điền các phần khác với base rules.

---

## 1. Tech Stack Override
```
# Điền tech stack riêng nếu khác base
Database: PostgreSQL | MongoDB | Redis | [khác]
ORM: EF Core | Dapper | [khác]
Auth: JWT | Cookie | [khác]
Message Bus: [nếu có — RabbitMQ, Kafka, ...]
External APIs: [danh sách nếu có]
```

---

## 2. Domain-specific Rules

> Mô tả các rule nghiệp vụ quan trọng mà agent cần biết.
> Ví dụ:

```
# Ví dụ cho service quản lý điểm sinh viên:
- Điểm phải trong khoảng 0–10, flag nếu không có validation range này.
- Không được phép sửa điểm nếu học kỳ đã kết thúc (IsTermClosed = true).
- Mọi thao tác thay đổi điểm phải có audit log.
```

---

## 3. Naming Override

> Nếu project dùng convention khác base (ví dụ: snake_case thay vì camelCase).

```
# Ví dụ:
- Variable/Parameter: snake_case (override base camelCase)
- API response field: snake_case (theo chuẩn OpenAPI của project)
```

---

## 4. Patterns bắt buộc trong project này

> Các pattern mà mọi code trong service này phải tuân theo.

```
# Ví dụ:
- Repository pattern bắt buộc: không được gọi DbContext trực tiếp từ Service layer.
- Mọi command/query phải đi qua MediatR handler.
- Response luôn wrap trong ApiResponse<T>, không return raw object.
```

---

## 5. Patterns bị cấm trong project này

```
# Ví dụ:
- Không dùng AutoMapper (đã quyết định dùng manual mapping).
- Không gọi HTTP trực tiếp từ domain layer, phải qua IExternalServiceClient.
- Không dùng static class cho business logic.
```

---

## 6. Severity Override

> Nếu muốn tăng/giảm severity cho một số rule cụ thể trong project này.

```
# Ví dụ:
- Missing ConfigureAwait(false): nâng lên CRITICAL (project này là library)
- Method > 30 dòng: giảm xuống LOW (team đã thống nhất 50 dòng)
```

---

## 7. Known Technical Debt (Bỏ qua khi review)

> Liệt kê các vấn đề đã biết, đã có ticket, và không cần flag lại trong review.

```
# Ví dụ:
- UserService.cs: inject 6 dependency — đã có ticket refactor [PROJ-123], bỏ qua.
- LegacyReportGenerator.cs: method dài > 100 dòng — sẽ rewrite trong sprint tới.
```

---

## 8. Test Requirements

> Yêu cầu riêng về test cho service này.

```
# Ví dụ:
- Mọi business logic method trong *Service.cs phải có unit test tương ứng.
- Không cần test cho controller layer (đã có integration test riêng).
```
