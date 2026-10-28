# Acadex Windows Client — Backend Compatibility Report

Audit source: `index.js` on `main` (SHA `f07c9d7d24fcabddd39a2b827e5336958b3c2127`) and `package.json` (SHA `be82613efb5be1013b9be7720758d7333a6516f4`). Source inspection only; no live endpoint tests performed.

## Authentication findings

- `POST /api/auth/register` and `POST /api/auth/login` accept JSON and return `{ token, user }` on success. Registration fields: `role` (`teacher` or `student`), `email`, `password`, `displayName`, and `studentId` for students.
- Tokens are 32 random bytes encoded as hex. Only SHA-256 hashes are stored in `auth_sessions`. Sessions expire after 30 days.
- Protected requests send `Authorization: Bearer <token>`. `getAuthUser` hashes the presented token and looks up a non-expired session. A native client can use this without browser cookies.
- Teacher/student restrictions use `requireRole`; routes also scope exams/folders/results to the owner or student account.
- `POST /api/auth/logout` deletes the current session hash. Password change does not visibly revoke other sessions.
- Browser exam-password authorization uses an HTTP-only, HMAC-signed `acadex_exam_gate` cookie. `POST /api/exam/:id/session` also accepts the exam password in its JSON body when creating a new attempt, so WPF can use the normal backend validation without relying on the browser-only cookie gate.
- No CSRF middleware/token was found in the inspected Express setup. Keep requests on HTTPS, use Bearer authorization, and never copy cookies from a browser.
- Treat the token as a long-lived secret. Store it using Windows DPAPI/Credential Manager; never log it or store it in plain config.

## Endpoint inventory

All protected JSON requests use `Authorization: Bearer <token>`. JSON requests should set `Content-Type: application/json`. The response schemas below summarize what is built in the source, not a formal OpenAPI schema.

| Method + route | Access | Request and response |
|---|---|---|
| `GET /ping` | Public | `{ ok: true, ts }`. Lightweight availability check. |
| `GET /api/health` | Public | `{ ok: true, ts, version }`. |
| `POST /api/auth/register` | Public | `{ role,email,password,displayName,studentId? }` -> `{ token,user:{id,role,email,displayName,studentId,className} }`. Validates role, email, password >=6, display name >=2, student class; duplicate email -> 409. |
| `POST /api/auth/login` | Public | `{ email,password }` -> `{ token,user:{id,role,email,displayName,studentId,className} }`; invalid credentials -> 401. |
| `POST /api/auth/logout` | Token optional | Deletes session hash for the provided bearer token; -> `{ok:true}`. |
| `GET /api/auth/me` | Any authenticated role | -> `{user:{id,role,email,displayName,studentId,className}}`; 401 if logged out/expired. |
| `POST /api/auth/change-password` | Any authenticated role | `{ currentPassword,newPassword }`; minimum new password length 6; -> `{ok:true}`. |
| `DELETE /api/auth/account` | Authenticated + password confirmation | `{password}`; destructively deletes account and associated owned exams/submissions/drafts. Must require explicit UI confirmation. |
| `GET /api/teacher/exams` | Teacher | -> `{exams:[...]}`; owner's exam metadata, attempt/submission counts, averages and settings. |
| `GET /api/teacher/exams/:id` | Owning teacher | Full exam detail: id,title,type,pdfDataUrl,questions,studentPassword,durationMs,createdAt,folder/settings/stats/url. Avoid logging sensitive exam password/content. |
| `POST /exam/create` | Teacher | Supports title,studentPassword,durationMs,type,pdfDataUrl,questions,folderId,allowRetake,maxAttempts,negativeMarking,passMarkPercent,resultVisibility,peersVisible,b2Key. -> `{examId,url}`. PDF uploads can use a B2 object key; template questions are validated MCQ/TF. |
| `PATCH /api/teacher/exams/:id` | Owning teacher | Updates title,studentPassword,durationMs,type,folderId,allowRetake,maxAttempts,negativeMarking,passMarkPercent,resultVisibility,peersVisible,archived and content fields when changed. -> `{ok:true,examId,url}`. |
| `DELETE /api/teacher/exams/:id` | Owning teacher | Deletes B2 object(s) and exam; -> `{ok:true,examId}`. Confirm explicitly. |
| `GET /api/teacher/folders` | Teacher | -> `{folders:[{id,name,createdAt,examCount}]}`. |
| `POST /api/teacher/folders` | Teacher | `{name}`; -> 201 `{folder:{id,name,createdAt}}`. Required, max 80 chars, case-insensitive duplicates rejected. |
| `PATCH /api/teacher/folders/:id` | Folder owner | `{name}`; -> `{folder:{id,name,createdAt}}`. |
| `DELETE /api/teacher/folders/:id` | Folder owner | Deletes folder; exams are left unfiled by DB FK; -> `{ok:true}`. |
| `GET /api/teacher/drafts` | Teacher | -> `{drafts:[{id,title,updatedAt,data}]}`, latest 50. |
| `PUT /api/teacher/drafts/:id` | Teacher | `{title,data}`; data must be a JSON object <=512 KiB; title <=120 chars; limit 50 drafts. -> `{id,updatedAt}`. |
| `DELETE /api/teacher/drafts/:id` | Teacher | -> `{ok:true}`. |
| `GET /api/teacher/exams/:id/results` | Owning teacher | -> `{exam,pdf,results:[...]}`; PDF shows attempts/submission status; templates include score/percentage/attempt and public result URL when available. |
| `GET /api/teacher/submissions/:id` | Owning teacher | Detailed template submission with exam/student details, score, answer/results and submittedAt. |
| `GET /api/teacher/students` | Teacher | -> `{students:[...]}`; students who submitted to this teacher's exams. |
| `GET /api/teacher/students/:studentId` | Teacher + student has submitted to teacher | Student summary, results and average. |
| `GET /api/teacher/students/:studentId/results/:submissionId` | Teacher, scoped to own exam + student | Detailed submission; may mint a public-result token. |
| `GET /api/student/results` | Student | -> `{results:[...]}`; hidden results expose only submitted/released state, score-only hides details. |
| `GET /api/student/results/:id` | Owning student | Result details governed by hidden/score_only/instant visibility. |
| `GET /api/student/exams/:examId/peers` | Student | Peer/rank statistics; blocked when peer visibility/results are hidden or no submission exists. |
| `GET /api/student/teachers` | Student | -> `{teachers:[{id,email,displayName}]}`, teachers whose exams the student has submitted to. |
| `GET /api/admin/my-storage` | Any logged-in user in current source | B2 storage report and owned exam information. Current handler does not explicitly enforce a role. |
| `GET /api/admin/db-size` | Any logged-in user in current source | -> `{usedBytes,version}`. Current handler does not explicitly enforce a role. |
| `POST /api/teacher/b2-presign` | Teacher | `{examId,contentType?}` -> `{uploadUrl,key}`; signed upload URL expires in one hour. |
| `POST /api/teacher/b2-multipart-start` | Teacher | `{examId,contentType?}` -> `{uploadId,key}`. |
| `POST /api/teacher/b2-multipart-part` | Teacher | `{key,uploadId,partNumber}` -> `{url}`. |
| `POST /api/teacher/b2-multipart-complete` | Teacher | `{key,uploadId,parts}` -> `{ok:true,key}`. |
| `POST /api/teacher/b2-multipart-abort` | Teacher | `{key,uploadId}` -> `{ok:true}`. |
| `GET /api/exam/:id` | Public metadata | Returns public exam metadata, omits pdfDataUrl and questions. |
| `GET /api/exam/:id/pdf` | Student bearer + active exam session | Requires `X-Exam-Session: <sessionToken>`; streams PDF, supports HTTP Range/206, checks ownership and expiry. |
| `GET /api/exam/:id/authorization` | Student bearer | -> `{authorized:boolean}`, reads browser cookie gate. WPF should not depend on this gate if using password in session creation. |
| `POST /api/exam/:id/authorize` | Student bearer + password | `{password}`; checks archive/password/already-completed state and sets HTTP-only cookie; -> `{authorized:true,expiresIn:86400}`. Browser-specific. |
| `POST /api/exam/:id/session` | Student bearer | Request supports password,sessionToken?,deviceId?,studentId?,studentName?,startAuthorized?. Response includes sessionToken,studentId,studentName,deviceId,startedAt,endAt,progress,public exam metadata and pdfUrl for PDF exams. Use existing sessionToken to resume. Server enforces password for a new attempt, archive/completion and session ownership. |
| `POST /api/exam/:id/progress` | Student bearer + valid session | `{sessionToken,progress:{answers,currentQuestion,pdfPage,pdfScrollRatio}}` -> `{ok:true,progress}`. Rejects closed/expired sessions. |
| `POST /api/exam/:id/finish` | Student bearer + valid session | `{sessionToken,answers}`; template exams graded server-side with result visibility enforced; PDF creates a submission record without automated grading. |
| `GET /api/exam/:id/attempts` | Student bearer | Own template attempts, percentages and public result URL when available. |
| `GET /api/exam/:id/result/:token` | Student bearer, own account | Fetches own template result, enforces ownership. |
| `GET /result/:publicToken` | Public token | Returns HTML result page, not JSON; honors hidden visibility. |
| `GET /exam/:id` | Public link | Returns the browser exam-taking HTML. Do not reuse it as the WPF UI. |

## Validation and integration notes

- Express runs `express.json({limit:'25mb'})`, accepts JSON, and uses `express.static` for the web app. Native HttpClient is not subject to browser CORS restrictions.
- Existing backend validation is the source of truth. Client-side validation should improve UX but must not bypass server checks.
- PDF exam workflow depends on `X-Exam-Session` plus the Authorization bearer token. Support byte ranges or stream the PDF to a temporary file safely; don't load huge files as base64 in memory.
- Teacher B2 upload uses presigned URLs and multipart routes. Do not embed B2 credentials in the executable.
- Avoid using web-only `/api/exam/:id/authorize` cookie gate; pass the exam password to `/api/exam/:id/session` for a new attempt.
- Account/exam deletion are destructive. Password change does not appear to revoke other sessions in this implementation; do not imply it does.
- UI must honor resultVisibility (hidden/score_only/instant), allowRetake/maxAttempts, archived state and server-side session expiry.
- Public `/exam/:id` is an HTML UI and not an API. Use JSON routes and implement all screens natively.

## Recommended implementation phases

1. Add configurable API base URL, typed DTOs and a single HttpClient service, plus bounded `/api/health` retries and clear server-unavailable UI.
2. Implement login/logout, secure token persistence and role-aware navigation.
3. Student MVP: results list/details; exam metadata/password/session; template taking with timer/progress autosave/resume/submit; PDF streaming.
4. Teacher MVP: exams, folders, drafts, results, student lists; then create/edit and B2 upload.
5. Add destructive operations last, behind explicit confirmations.

## Verification status

This is a source audit only. No live API call, no real account, no backend integration test, and no native build was run during this audit.