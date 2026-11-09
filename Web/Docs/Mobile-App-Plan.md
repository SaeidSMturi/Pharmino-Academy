# Pharmino Academy mobile app: screens and API plan

_Written 9 November 2026 from the current code in `Pharmino_Web` and the Flutter prototype in `pharmino_academy`._

---

## 1. Where things stand today

| Piece | State |
|---|---|
| **Flutter prototype** (`pharmino_academy/`, Oct 2026) | About 3,400 lines: splash, login, register, home, course details, my courses, player, profile. **It's mostly mock-up.** The player only simulates playback, and about half the server addresses it calls (`/api/auth/profile`, `/api/courses/my-courses`, `/api/courses/enroll`, `/api/videos/...`) don't exist. It supports English and Arabic but not Farsi, and uses the old gold/navy colours instead of the current PA brand. |
| **Mobile API on the server** (`Controllers/Api/`) | Only 4 endpoints exist: login, register, list courses and course details. **These aren't safe to build on** (see 1.1). |
| **Website student area** | Complete: courses and bundles, encrypted HLS video with watermark, progress, watermarked PDFs, timed tests, payments, profile, active-device management. The app reuses all of this logic. |

### 1.1 Problems in the existing mobile API (live now)

1. **`POST /api/auth/login` returns a random token that the server never stores or checks.** No one can use it to reach anything protected. It's harmless, but useless.
2. **`POST /api/auth/register` creates accounts without the email-code check** that the website's sign-up enforces. Anyone can create unverified accounts through it today.
3. **`GET /api/courses/{id}` exposes each lesson's internal video file name.** That name is the storage folder for the encrypted video. The video stays protected by signed links, but there's no reason to hand it out.
4. **No request rate limiting** on login or sign-up (web or API). Accounts do get locked after failed passwords, but nothing slows down one address trying many accounts or requesting many email codes.

**Recommendation:** remove or disable the old `/api/auth/*` and `/api/courses*` endpoints now (the prototype is the only client that calls them), and build the new API under `/api/v1/` as described below.

---

## 2. Framework: updated recommendation

Earlier I suggested React Native. **Now that a Flutter prototype exists, I recommend staying with Flutter.** The API work below is identical either way, and Flutter covers everything this app needs:

| Need | Flutter package |
|---|---|
| Encrypted HLS video (AES-128), with custom request headers | `video_player` (AVPlayer on iOS, ExoPlayer on Android) |
| Blocking screenshots and recording (Android), detecting recording (iOS) | `screen_protector` |
| Secure token storage | `flutter_secure_storage` (Keychain / Keystore) |
| HTTP with automatic token refresh | `dio` with an interceptor |
| Navigation and deep links | `go_router` |
| State | Keep `provider` (already used) or move to `riverpod` |
| English, Arabic and Farsi, right-to-left | `flutter_localizations` + `gen-l10n` (ARB files) |
| Push notifications | `firebase_messaging` |
| In-app purchases (if chosen, see 6.1) | `purchases_flutter` (RevenueCat) |
| **Building iOS from Windows** | **Codemagic** (cloud Mac builds and App Store upload) |

The prototype's screens are worth keeping as a starting layout, but the services, models and player need to be rewritten against the new API, and the theme changed to the PA brand.

---

## 3. App screens

### Public (not signed in)
| Screen | Matches website |
|---|---|
| Splash, plus first-run language picker (EN / AR / FA) | Language switcher |
| Sign in | `/Account/Login` |
| Sign up in 3 steps: details → 6-digit email code → done | `/Signup/*` one-page sign-up |
| Forgot password → code → new password | `/Account/ForgotPassword`, `ResetPassword` |
| Course catalog | `/Courses` |
| Course details: description, price, curriculum, free preview lessons | `/Course/{id}` |

### Signed in (bottom tabs: **Home · My Courses · Tests · Profile**)
| Screen | Matches website |
|---|---|
| **Home**: "Continue watching", progress stats, recent courses | Student dashboard |
| **My Courses**: courses and bundles with progress | `/Students/Courses` |
| **Course**: lessons with ✓/progress, PDFs, tests | `/Students/Course/{id}` |
| **Bundle**: list of the courses inside | Bundle details |
| **Player**: resume position, next lesson, speed, quality, moving watermark (masked email + forensic code), full screen | `/Students/Courses/Watch` |
| **PDF viewer**: page images with the burned-in watermark, pinch-zoom | Secure PDF viewer |
| **Tests**: list with past attempts and scores | `/Students/Tests` |
| **Take test**: timer, one question per page, auto-saved answers, finish | `Tests/Take` |
| **Result**: score, pass/fail, review with correct answers and explanations | `Tests/Result` |
| **Profile**: name, email, phone, edit, change password | Profile |
| **My devices**: signed-in devices, sign out any of them | Active sessions |
| **Payments**: purchase history | Finance |
| **Settings**: language, light/dark, notifications, support email, privacy policy, **delete account** | — |

**Stays web-only:** the whole admin area, webmail, SEO tools.

---

## 4. Sign-in design (the main server change)

The website uses a cookie that's locked to the browser's IP address. Phones switch between Wi-Fi and mobile data all the time, so the app needs tokens bound to the **device** instead.

- **Access token**: a JWT valid for 15 minutes, sent as `Authorization: Bearer ...`. It carries `userId` and `sessionId`.
- **Refresh token**: a random value valid for 30 days. The server stores only a hash of it. It's replaced with a new one each time it's used, and reusing an old one signs that device out.
- **Device ID**: the app generates a random ID on first launch and keeps it in secure storage. Refresh only works from the same device ID.
- **One session table for web and app.** Extend the existing `UserActiveSession` with `ClientType` (Web/Mobile), `DeviceId`, `DeviceName`, `RefreshTokenHash`, `RefreshExpiresAtUtc`. That means:
  - the existing 1-login limit (`MaxConcurrentLogins`) covers phones too: signing in on the phone signs the browser out (see decision 6.3),
  - phones appear in the website's **Active sessions** page and can be signed out from there, and vice versa,
  - signing a device out takes effect within 15 minutes at most (access tokens are also checked against the session table on each request, so in practice it's immediate).
- Add JWT Bearer as a **second** authentication scheme. The website keeps its cookie unchanged, and `/api/v1/*` controllers accept only Bearer.
- Add **rate limiting** (built into ASP.NET Core) to login, sign-up code, forgot-password and refresh, on both the web and the API.

---

## 5. API endpoints (`/api/v1`)

All responses are JSON. Errors use one standard shape (`ProblemDetails`). The `Accept-Language: en|ar|fa` header picks the language of messages.

### 5.1 Account and sign-in
| Method | Path | Purpose |
|---|---|---|
| POST | `/auth/signup/check-email` | Is this email free? |
| POST | `/auth/signup/send-code` | Email a 6-digit code (reuses `SignupVerificationService`) |
| POST | `/auth/signup/verify-code` | Check the code → sign-up ticket |
| POST | `/auth/signup` | Create the account with the ticket → tokens |
| POST | `/auth/login` | `{email, password, deviceId, deviceName, platform}` → `{accessToken, refreshToken, expiresIn, user}` |
| POST | `/auth/refresh` | `{refreshToken, deviceId}` → new pair |
| POST | `/auth/logout` | Ends this device's session |
| POST | `/auth/forgot-password` | Email a reset code |
| POST | `/auth/reset-password` | Code + new password |

### 5.2 Me
| Method | Path | Purpose |
|---|---|---|
| GET / PUT | `/me` | Profile |
| POST | `/me/change-password` | |
| DELETE | `/me` | **Delete account** (Apple requires this when sign-up happens in the app) |
| GET | `/me/dashboard` | Stats, continue-watching, recent courses |
| GET | `/me/sessions` | Signed-in devices (web and phone) |
| DELETE | `/me/sessions/{id}` · `/me/sessions` | Sign one out · sign all others out |
| GET | `/me/payments` | Purchase history |
| POST / DELETE | `/me/push-token` | Register or remove this device for push |
| GET | `/me/notifications` | Notification list |

### 5.3 Catalog (no sign-in needed)
| Method | Path | Purpose |
|---|---|---|
| GET | `/catalog/courses` | Listed, active courses: title, image URL, price, discount, lesson count, bundle flag |
| GET | `/catalog/courses/{id}` | Details, curriculum titles and lengths, free-preview flags. **No file names.** |

### 5.4 Learning
| Method | Path | Purpose |
|---|---|---|
| GET | `/me/courses` | Enrolled courses and bundles with progress and next lesson |
| GET | `/me/courses/{id}` | Lessons with progress, PDFs, tests, enrollment; for a bundle, its courses |
| POST | `/videos/{id}/playback` | **Playback grant** (5.5) |
| GET / PUT | `/videos/{id}/progress` | Resume position, completed flag (reuses `SaveVideoProgressAsync`) |
| POST | `/pdfs/{id}/open` | `{pageCount, watermarkCode, maskedEmail, viewToken}` |
| GET | `/pdfs/{id}/pages/{n}?t=` | One watermarked page as JPEG (reuses `SecurePdfRenderer`) |

### 5.5 Video playback on phones
The existing encrypted HLS pipeline (signed per-segment links, per-user A/B forensic segments, AES key endpoint) is reused. A mobile video player can't refresh links mid-video the way the website's player does, so:

1. `POST /videos/{id}/playback` checks enrollment and returns:
   `{ manifestUrl, headers: { "X-Video-Session": sid }, watermark: { code, maskedEmail }, resumeAt, expiresAt }`
2. A **mobile HLS endpoint set** (`/api/v1/hls/{videoId}/manifest | segment | key`) shares the playlist-rewriting code with the current `HlsController`. It differs in three ways:
   - It takes the user from the **signed video session ID** (already HMAC-signed, containing userId and videoId) instead of the browser cookie, so playback doesn't break when the 15-minute access token expires mid-lesson.
   - It also checks that the device's session is still active, so signing out a phone stops its playback.
   - The AES key link lasts as long as the playback grant (about 4 hours) instead of 5 minutes, because iPhones re-fetch the key when seeking or switching quality.
3. **Watermark**: the app draws the same moving masked-email and code overlay as the website, and the A/B forensic segments still identify the viewer if a recording leaks.
4. **Screen recording**: Android blocks screenshots and recording on the player and PDF screens. On iOS the app detects recording or mirroring, hides the video and logs it through the existing suspicious-activity service.

> **To test early on real phones:** whether iOS sends the `X-Video-Session` header on every segment and key request, and whether the anti-download behaviour analyser (`AdvancedBehaviorAnalyzer`) mistakes the phone players' normal prefetching for a downloader and blocks the stream. Its thresholds may need a separate mobile profile.

### 5.6 Tests
| Method | Path | Purpose |
|---|---|---|
| GET | `/tests` | Available tests (same access rules as the website) and the user's past attempts |
| POST | `/tests/{id}/attempts` | Start or resume → attempt, questions in their shuffled order **without correct answers**, seconds remaining |
| PUT | `/attempts/{id}/answers/{questionId}` | `{optionId}`; refused after the time limit |
| POST | `/attempts/{id}/finish` | Grade → score, pass/fail |
| GET | `/attempts/{id}` | Review (only once finished): correct answers and explanations |

The grading and time-limit logic moves out of the web `TestsController` into a shared service, so the website and the app grade exactly alike.

### 5.7 App housekeeping
| Method | Path | Purpose |
|---|---|---|
| GET | `/app/config` | Minimum app version (forces an update when needed), maintenance flag, support email, whether in-app purchase is turned on |

---

## 6. Decisions needed from you

### 6.1 Payments in the app
Apple (and Google Play) require their own in-app purchase system for digital content bought inside an app. Online courses count, so Lemon Squeezy can't be used inside the app.

| Option | Upside | Downside |
|---|---|---|
| **A. In-app purchase through RevenueCat** (recommended) | Lowest review risk; one-tap purchase | Apple/Google take 15% (small-business rate) to 30%; prices follow store price points. Server needs `POST /webhooks/revenuecat` → creates the same enrollment as a Lemon Squeezy payment. |
| **B. Watch-only app**: no prices, no buy button, no link to the website | Keeps 100% of revenue; simplest | Apple decides case by case whether course apps qualify, so a rejection is possible. |

### 6.2 Framework
Keep **Flutter** (recommended, prototype exists) or switch to React Native.

### 6.3 Device limit
Does a phone count toward the current limit of 2 simultaneous logins? Options: keep 2 total, or allow 2 browsers + 1 phone.

### 6.4 Offline downloads
**Not in version 1** (recommended). Watching downloaded lessons offline safely needs proper DRM (Apple FairPlay + Google Widevine), which means a paid DRM service and re-processing the videos. Worth considering later if students ask for it.

---

## 7. Order of work

**Phase 1: server foundation**
1. Disable the old `/api/auth/*` and `/api/courses*` endpoints; add rate limiting.
2. JWT + refresh tokens + `UserActiveSession` columns (one database migration).
3. `/api/v1` account, me, catalog and learning endpoints.
4. Mobile HLS endpoints and playback grant; mobile PDF tokens bound to the device, not the IP.

**Phase 2: app version 1**
5. Restyle the prototype to the PA brand (light and dark), add Farsi.
6. Sign in / sign up / forgot password, token storage and refresh.
7. Catalog, My Courses, Course, Player (real playback, watermark, progress), PDF viewer.
8. Profile, My devices, Settings, Delete account.
9. Test on real iPhone and Android phones (video headers, behaviour analyser, recording protection).

**Phase 3: complete**
10. Tests (take, timer, review).
11. Purchases (option A or B), payment history.
12. Push notifications (admin "send notification" also pushes to phones).

**Phase 4: store release**
13. Apple Developer account ($99/year), Google Play account ($25 once), privacy-policy page, store screenshots in EN/AR, review demo account, Codemagic build pipeline.
