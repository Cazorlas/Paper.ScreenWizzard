---
name: verification-before-completion
description: Use when about to claim work is complete, fixed, or passing, and before committing or creating PRs.
---

# Verification Before Completion

## Overview


**Core principle:** Evidence before claims, always.


## The Iron Law

```
NO COMPLETION CLAIMS WITHOUT FRESH VERIFICATION EVIDENCE
```

If you haven't run the verification command in this message, you cannot claim it passes.

## The Gate Function

```
BEFORE claiming any status or expressing satisfaction:

1. IDENTIFY: What command proves this claim?
2. RUN: Execute the FULL command (fresh, complete)
3. READ: Full output, check exit code, count failures
4. VERIFY: Does output confirm the claim?
   - If NO: State actual status with evidence
   - If YES: State claim WITH evidence
5. ONLY THEN: Make the claim

```

**A passing test only counts once it has been seen failing.** A test written for behaviour that is
already correct is green on its first run and has proved nothing yet - it may not be checking what it
looks like it is checking. Break that one rule in the production code, watch it go red on its
assertion, restore the code, and quote the red line in the evidence. A suite that went from green to
green is a number, not a verification (skill `task-do`, "a test green on its first run is not yet a
test").

## Common Failures

| Claim | Requires | Not Sufficient |
|-------|----------|----------------|
| Tests pass | Test command output: 0 failures **and** an executed count above zero | Previous run, "should pass", "Passed" over 0 tests |
| Linter clean | Linter output: 0 errors | Partial check, extrapolation |
| Build succeeds | Build command: exit 0 | Linter passing, logs look good |
| The new code is running | Reload result, loaded assembly identity or hash, behaviour only the new build has | A successful load message |
| It works in the host | State read back from the host after the run, a capture for anything visible | A success message, a completion marker, a "loaded" reply |
| Bug fixed | Test original symptom: passes | Code changed, assumed fixed |
| Regression test works | Red-green cycle verified | Test passes once |
| Agent completed | VCS diff shows changes | Agent reports "success" |
| Requirements met | Line-by-line checklist | Tests passing |

**Keep the lanes distinct when reporting.** Unit tests, a headless host run, a build or package, and the live
host each prove different things. Say which ran and which did not, and why - "built and unit-tested; not run
in the host" is a true sentence, never a pass for the host lane.

## Red Flags - STOP

- Using "should", "probably", "seems to"
- Expressing satisfaction before verification ("Great!", "Perfect!", "Done!", etc.)
- About to commit/push/PR without verification
- Trusting agent success reports
- Relying on partial verification
- Thinking "just this once"
- Tired and wanting work over
- **ANY wording implying success without having run verification**

## Rationalization Prevention

| Excuse | Reality |
|--------|---------|
| "Should work now" | RUN the verification |
| "I'm confident" | Confidence ≠ evidence |
| "Just this once" | No exceptions |
| "Linter passed" | Linter ≠ compiler |
| "Agent said success" | Verify independently |
| "I'm tired" | Exhaustion ≠ excuse |
| "Partial check is enough" | Partial proves nothing |
| "Different words so rule doesn't apply" | Spirit over letter |

## Key Patterns

**Tests:**
```
✅ [Run test command] [See: 34/34 pass] "All tests pass"
❌ "Should pass now" / "Looks correct"
```

**Regression tests (TDD Red-Green):**
```
✅ Write → Run (pass) → Revert fix → Run (MUST FAIL) → Restore → Run (pass)
❌ "I've written a regression test" (without red-green verification)
```

**Build:**
```
✅ [Run build] [See: exit 0] "Build passes"
❌ "Linter passed" (linter doesn't check compilation)
```

**Requirements:**
```
✅ Re-read plan → Create checklist → Verify each → Report gaps or completion
❌ "Tests pass, phase complete"
```

**Agent delegation:**
```
✅ Agent reports success → Check VCS diff → Verify changes → Report actual state
❌ Trust agent report
```

## Bàn giao thay đổi và nội dung PR

Khi bàn giao thay đổi hoặc người dùng yêu cầu nội dung PR, thêm vào báo cáo:

- **Tóm tắt:** câu ngắn hoặc sơ đồ nhỏ làm rõ thay đổi; dùng từ vựng dự án.
- **Trước / sau:** link bằng chứng đã đọc, test đỏ rồi xanh hoặc ảnh/giá trị; thiếu thì nêu khoảng trống.
- **Phạm vi ảnh hưởng:** người dùng, consumer, dữ liệu hoặc host bị chạm; chưa biết thì nói chưa biết.
- **Hoàn tác:** cách trở lại và điều kiện của nó. Quay lại bản chương trình cũ không tự **khôi phục dữ liệu**
  đã xoá; nêu backup/migration ngược, bước đã kiểm và bước chưa kiểm. Thiếu bằng chứng thì không khẳng định
  hoàn tác an toàn. Chỉ đọc và mô tả, không tự chạy hoàn tác, commit, mở PR hay deploy.

## When To Apply

Apply before any claim that work is done, fixed or passing, in any wording, and before committing, opening a PR, ticking a task or delegating.
