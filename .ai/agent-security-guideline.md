# Agent Security Guideline for DartSuite

## Always Enforce These Security Principles

This guideline is **mandatory** for every implementation, code change, or architectural decision made by the agent for the DartSuite project. It must be considered at the start of every new session and for every action taken.

---

### 1. Secure Communication
- All data transfers between components **must use HTTPS** to ensure confidentiality and integrity.
- Never allow unencrypted (HTTP) communication between backend, frontend, or external services.

### 2. Authentication & Authorization
- The API must implement **JWT-based authentication** and **role-based access control (RBAC)** for all endpoints.
- Only authenticated and authorized users may access protected resources.
- Validate all user input to prevent injection and other attacks.
- Implement rate limiting to prevent abuse.

### 3. Data Protection
- The database must only be accessible via the API—**no direct external access**.
- All database connections must be encrypted.
- Sensitive data must be stored encrypted.

### 4. Frontend Security
- The Blazor Server frontend must communicate with the API only via HTTPS.
- Use the same authentication and authorization mechanisms as the backend.

### 5. Chrome Extension
- The Chrome extension communicates securely via the API or the Autodarts site.
- No special authentication is required for the extension, but all communication must be secure.

### 6. Autodarts.io Login Handling
- Login credentials for autodarts.io are **never stored in DartSuite**.
- If a "remember me" feature is used, credentials are stored encrypted in browser storage and only for the session duration.
- Only authenticated users can access personal features; guests are limited to tournament-specific access.

### 7. Guest & Participant Access
- Guests can access tournaments via a code or TournamentId, but only see information relevant to themselves and public tournament data.
- Guests cannot access personal or administrative features.
- The landing page is the only page accessible without login, and only via a TournamentId parameter.

### 8. Privacy & Data Minimization
- Participants and guests only see information directly relevant to them and global tournament info.
- Board details and other sensitive data are never exposed to unauthorized users.

---

**This security guideline must be enforced in every session and for every agent action.**
