import type { SaveScrapeResultData } from '../../shared/contracts/messages';
import type { ScrapeResult } from '../../shared/models/scrapeResult';
import { getStoredAccessToken } from '../auth/sessionStorage';
import { API_BASE_URL } from './apiConfig';
import { toApiScrapeResultPayload } from './scrapeResultPayload';

const SCRAPE_RESULTS_ENDPOINT = `${API_BASE_URL}/scrape-results`;

export interface ScrapeResultGateway {
  send(result: ScrapeResult): Promise<SaveScrapeResultData>;
}

async function buildErrorMessage(response: Response): Promise<string> {
  const responseText = await response.text();
  const trimmedResponseText = responseText.trim();

  if (trimmedResponseText.length === 0) {
    return `ASP.NET API returned ${response.status} ${response.statusText}.`;
  }

  return `ASP.NET API returned ${response.status} ${response.statusText}: ${trimmedResponseText}`;
}

export class AspNetApiClient implements ScrapeResultGateway {
  async send(result: ScrapeResult): Promise<SaveScrapeResultData> {
    const accessToken = await getStoredAccessToken();

    if (!accessToken) {
      throw new Error(
        'Sign in to ApplyVault before saving from the extension. Open the popup to refresh your session if you are already signed in.'
      );
    }

    const response = await fetch(SCRAPE_RESULTS_ENDPOINT, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${accessToken}`
      },
      body: JSON.stringify(toApiScrapeResultPayload(result))
    });

    if (!response.ok) {
      throw new Error(await buildErrorMessage(response));
    }

    const payload = (await response.json()) as Partial<SaveScrapeResultData>;

    if (typeof payload.id !== 'string' || typeof payload.savedAt !== 'string') {
      throw new Error('ASP.NET API returned an invalid save response.');
    }

    return {
      id: payload.id,
      savedAt: payload.savedAt
    };
  }
}
