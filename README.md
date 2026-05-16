# government-petition-service
University project for people to send petitions to the government with ability to filter it according to themes or topics.

## Gemini API Integration (MVP)

Scenario:
- AI-assisted petition drafting in the create form.
- User enters a rough draft, and the backend asks Gemini to suggest a normalized title, content, and category.

Configuration:
- Set environment variable `Gemini__ApiKey` with your Google Gemini API key.
- Optional config (in `PetitionService.Server/appsettings.json` under `Gemini`):
	- `Model`
	- `TimeoutSeconds`
	- `MaxRetries`

Server endpoint:
- `POST /api/petitions/ai-draft`
- Requires authenticated user role (`User` or `Admin`).
- Rate limited (fixed window) to protect the external API.

## Run MinIO with Docker Compose

1. Copy env template:
	- `copy .env.example .env` (Windows)
2. Start MinIO:
	- `docker compose up -d`
3. Open MinIO Console:
	- `http://localhost:9001`
4. Login with values from `.env`.

Current backend settings expect MinIO at:
- Endpoint: `http://localhost:9000`
- Bucket: `petition-attachments`

Stop MinIO:
- `docker compose down`
