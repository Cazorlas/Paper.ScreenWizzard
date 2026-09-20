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
  "worktree": { "copyFiles": ["<file cục bộ ngoài git>"] }
}
```

Verb không khai → exit 5, lane đó báo `không áp dụng`; dự án không sửa model thì bỏ `model` khỏi `lanes` và
`workTypes`. Muốn đổi quy trình cho mọi dự án: sửa trong kit, chạy test của kit, nâng phiên bản, chạy lại
setup ở từng dự án.
