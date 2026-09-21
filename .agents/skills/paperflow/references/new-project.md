# Mẫu dự án mới dùng `/paperflow` thế nào

Không sửa skill này. Một mẫu dự án mới dùng được sau hai việc:

1. **Setup của kit** với host của dự án: chép skill này, `task-*`, `model-task`, lane agent, runner
   `paperflow`, hook, và các skill của gói host (skill live, skill model nếu có).
2. **Một file cấu hình** `.claude/paper.profile.json`:

```json
{
  "hosts": ["<host>"],
  "lanes": ["unit", "ui", "live", "model"],
  "verbs": { "build": "<lệnh build>", "test": "<lệnh test>", "ui": "<lệnh test UI>", "publish": "<lệnh đưa build vào host đang mở>" },
  "knownFailures": "<đường dẫn danh sách đỏ đã biết, có thể chứa {config}>",
  "docsSource": "<mcp:server | url | xmldoc:thư mục>",
  "workTypes": {
    "code":  { "lanes": ["unit", "ui", "live"] },
    "model": { "lanes": ["model"], "rules": "docs/features/**/RULE.md", "samples": "docs/features/**/SAMPLE", "references": "docs/features/**/REFERENCE.md" }
  },
  "laneAliases": { "<tag lane cũ>": "live" },
  "models": { "strong": "<model mạnh nhất>", "strongOnAccount": "<email tài khoản duy nhất được đổi model>" },
  "worktree": { "copyFiles": ["<file cục bộ ngoài git>"] },
  "live": {
    "loop": "<script vòng đo-trước: nhận một bước ensure | measure | red | green | verify | loop>",
    "dialogGuard": "<lệnh canh hộp thoại của host trong lúc có lời gọi>",
    "enforceOrder": false
  },
  "routing": {
    "workTypes": { "code": { "keywords": ["<từ của dự án cho việc code>"] }, "model": { "keywords": ["<từ cho việc model>"] } },
    "hosts": { "<host>": { "keywords": ["<tên sản phẩm, tên tính năng>"] } }
  }
}
```

Verb không khai → exit 5, lane đó báo `không áp dụng`; dự án không sửa model thì bỏ `model` khỏi `lanes` và
`workTypes`. Muốn đổi quy trình cho mọi dự án: sửa trong kit, chạy test của kit, nâng phiên bản, chạy lại
setup ở từng dự án.

`live.loop` là lời khai "host này tự kiểm được code": có nó thì việc code đi **đo trên host → đỏ → xanh → đo
lại** (luật 11), hook `live-first-guard` nhắc khi code bị sửa trước dòng `baseline` (chặn khi `enforceOrder`
là `true`). `routing` chỉ **thêm** từ khoá cho hook `paperflow-route`; bỏ trống thì hook vẫn định tuyến bằng
từ chung của từng loại việc và file route của gói host (`.claude/paperflow/routes/<host>.json`).
`"useDefaults": false` trong `routing` bỏ hai nguồn đó, chỉ dùng từ của dự án. Mỗi từ là một mảnh biểu thức
chính quy, khớp nguyên từ, không phân biệt hoa thường.
