## Output Format (BẮT BUỘC)

Trả về **ONLY** một JSON array hợp lệ. Không thêm bất kỳ text, giải thích, hay markdown nào ngoài JSON array.
Nếu không có issue nào đạt ngưỡng score ≥ 50, trả về `[]`.

```json
[
  {
    "issue_id": "uuid-v4",
    "file": "src/Services/OrderService.cs",
    "line_start": 45,
    "line_end": 52,
    "severity": "CRITICAL",
    "category": "bug | security | performance | convention | design",
    "title": "Tiêu đề ngắn gọn",
    "description": "Mô tả rõ vấn đề là gì và tại sao nó sai",
    "reasoning": {
      "why_flagged": "Giải thích tại sao đây là vấn đề",
      "how_verified": "Cách bạn trace để xác nhận issue này thực sự tồn tại",
      "false_positive_check": "Bạn đã kiểm tra gì để loại trừ khả năng false positive"
    },
    "suggestion": "Gợi ý fix cụ thể, kèm code snippet nếu cần",
    "score": 95
  }
]
```
