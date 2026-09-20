---
name: task-worktree
description: Run one planned task in its own git worktree beside the repository - create it on branch task/slug, carry the main checkout's uncommitted work across when the task needs it, take a baseline build and test there, do the work and verify it there, and merge back only when the user asked to merge, with the tests re-run on the target branch before the worktree is removed. Use when a task with a plan should not block other sessions with its build or its red tests, when the user asks to work in a worktree or in isolation, or when a task's worktree is finished and should be merged or discarded.
---

# task-worktree

Một việc, một worktree, một nhánh `task/<slug>`. **Build và test đỏ của việc nằm trong worktree**, nên
phiên khác đang làm ở bản checkout chính không bị chặn bởi file khoá, output build hay baseline đỏ của nó.

Mọi bước là một lệnh của `.claude/paperflow/worktree.ps1`. Exit: **0** ok · **1** từ chối, lệch, gộp
hỏng hay test đỏ · **2** sai tham số · **5** không áp dụng (ghi lại rồi đi tiếp, không phải lỗi).

**Không dùng công cụ worktree tích hợp của Claude Code** (`EnterWorktree`, subagent `isolation`). Nó đặt
bản checkout **bên dưới** kho, và mọi đường dẫn tương đối tính từ gốc kho — tham chiếu build, thư mục
dùng chung — trỏ sai chỗ từ đó.

## 1. Tạo

Slug kebab-case theo việc (`fix-join-order`), thường là slug của plan.

```powershell
.claude/paperflow/worktree.ps1 create <slug> [-From <branch>]
```

- Nhánh `task/<slug>` tạo từ nhánh hiện tại, hoặc `-From`. Nhánh gốc được ghi lại để `done` biết gộp về đâu.
- Chỗ đặt: `<parent>\_worktrees\<repo>-<slug>` khi thư mục `_worktrees` đó đã có, không thì
  `<parent>\<repo>-<slug>` cùng cấp kho — cùng độ sâu với kho, nên đường dẫn tương đối vẫn đúng.
- Các file cục bộ không nằm trong git mà profile khai ở `worktree.copyFiles` được chép theo nếu có; thiếu
  thì bỏ qua im lặng. Những file đó phải nằm trong `.gitignore`, không thì `done` coi chúng là thay đổi
  chưa commit.

## 2. Mang thay đổi chưa commit sang (chỉ khi việc cần)

```powershell
.claude/paperflow/worktree.ps1 carry <slug>
```

Chép `git diff HEAD --binary` và các file untracked của bản checkout chính vào worktree (worktree phải
sạch), rồi so từng file **bỏ qua CR**. Lệch một file là exit 1 kèm tên file: **dừng**, xem file đó, không
làm tiếp trên bản chép sai. `carry <slug> -Check` chỉ so, không chép.

Bản chính vẫn giữ các thay đổi đó. Trước `done -Merge`, bản chính phải bỏ hay commit chúng, không thì
git từ chối gộp đè lên.

## 3. Baseline trước khi làm

```powershell
.claude/paperflow/worktree.ps1 baseline <slug>
```

Chạy verb `build` rồi `test` của profile **trong worktree**, in dòng số test đã chạy. Test đỏ ở đây là
**baseline của việc**: ghi các test đỏ vào plan trước khi sửa gì, để đỏ sau này so được với đỏ có sẵn.
Mã thoát chính là verdict của lượt chạy (F5): **0** có test chạy và xanh, **1** có test chạy mà đỏ (hay
build đỏ), **4** không bài test nào chạy → **không kiểm được, không phải đạt**: chạy lại đúng một lần, lần
hai ghi lý do môi trường, đừng làm tiếp trên một baseline không kiểm được. Profile không khai cả `build`
lẫn `test` → exit 5.

## 4. Làm và kiểm trong worktree

Mở phiên làm việc tại thư mục worktree (in ra ở bước 1), rồi chạy như mọi việc có plan: `/task-do`
(plan, các lane) → `/task-verify`. Commit trên nhánh `task/<slug>`.

## 5. Xong

**Gộp chỉ khi người dùng đã bảo gộp.** Không bảo thì dừng ở bước 4 và báo nhánh, đường dẫn, kết quả verify.

```powershell
.claude/paperflow/worktree.ps1 done <slug> -Merge [-Test <command line>]
.claude/paperflow/worktree.ps1 done <slug> -Discard
.claude/paperflow/worktree.ps1 done <slug>
```

Chạy từ bản checkout chính, không từ trong worktree.

- `-Merge`: bản chính phải đang ở nhánh gốc. Gộp `task/<slug>` vào đó (fast-forward hoặc merge commit),
  chạy test **ở nhánh đích** (`-Test`, mặc định verb `test` của profile), xanh mới xoá worktree và nhánh.
  Test đỏ → **giữ worktree**, exit 1: báo test đỏ, không dọn. Gộp xung đột → huỷ gộp, giữ worktree.
- Không cờ: chỉ dọn khi không còn gì chưa gộp. Nhánh còn commit chưa có ở nhánh gốc → **từ chối**, nêu
  nhánh và số commit. Worktree còn thay đổi chưa commit → từ chối.
- `-Discard`: bỏ hết — chỉ khi người dùng xác nhận bỏ.

## 6. Báo cáo

Nhánh và đường dẫn worktree, dòng số test của baseline, kết quả `/task-verify`, và kết quả `done`: đã gộp
vào đâu với dòng số test ở nhánh đích, hay worktree còn giữ và vì sao.
