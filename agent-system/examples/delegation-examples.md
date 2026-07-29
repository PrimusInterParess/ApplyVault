# Delegation examples — ApplyVault

## Example: Angular search filter bug

- **assigned_agent:** `frontend-engineer`
- **include_paths:** `frontend/applyvault-jobs-ui/src/app/features/job-search/`
- **contracts:** eures-jobnet-search (UI only)
- **DoD:** Repro fixed; relevant spec updated if present; handoff lists files

## Example: Scrape DTO field

- **assigned_agent:** `backend-engineer`
- **collaborator:** `browser-extension-engineer`
- **include_paths:** `api/ApplyVault.Api/`, `extension/src/`
- **contracts:** scrape-ingest
- **constraint:** Preserve tenancy; no secret logging

## Example: Gemini CV import prompt

- **assigned_agent:** `ai-llm-engineer`
- **collaborator:** `backend-engineer`
- **include_paths:** `api/ApplyVault.Api/Services/CvDocuments/`
- **contracts:** google-ai-gemini-http, cv-section-catalog
- **constraint:** Generate guidance from catalog; never hardcode section types ad hoc
