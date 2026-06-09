import { MessageType, type ExtractPageTextRequest, type ExtractPageTextResponse } from '../../shared/contracts/messages';
import {
  getTabFrames,
  injectContentScript,
  sendMessageToFrame,
  type TabFrame
} from './chromeMessagingAdapter';

const MISSING_RECEIVER_ERROR = 'Receiving end does not exist';

function shouldRetryAfterMissingReceiver(response: ExtractPageTextResponse): boolean {
  return !response.success && response.error.includes(MISSING_RECEIVER_ERROR);
}

async function requestExtractionFromFrames(
  tabId: number,
  message: ExtractPageTextRequest
): Promise<Array<{ frame: TabFrame; response: ExtractPageTextResponse }>> {
  const frames = await getTabFrames(tabId);

  return Promise.all(
    frames.map(async (frame) => ({
      frame,
      response: await sendMessageToFrame(tabId, frame.frameId, message)
    }))
  );
}

function shouldRetryAfterMissingReceivers(
  responses: Array<{ frame: TabFrame; response: ExtractPageTextResponse }>
): boolean {
  return responses.length > 0 && responses.every((entry) => shouldRetryAfterMissingReceiver(entry.response));
}

export async function collectFrameExtractionResponses(
  tabId: number
): Promise<Array<{ frame: TabFrame; response: ExtractPageTextResponse }>> {
  const request: ExtractPageTextRequest = {
    type: MessageType.ExtractPageText
  };

  const initialResponses = await requestExtractionFromFrames(tabId, request);

  if (!shouldRetryAfterMissingReceivers(initialResponses)) {
    return initialResponses;
  }

  await injectContentScript(tabId);
  return requestExtractionFromFrames(tabId, request);
}
