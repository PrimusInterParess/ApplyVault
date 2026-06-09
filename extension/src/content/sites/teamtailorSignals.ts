export function hasInactiveListingSignal(documentRef: Document, text: string): boolean {
  const hostname = documentRef.location.hostname.toLowerCase();

  if (!hostname.includes('teamtailor.com')) {
    return false;
  }

  return /\b(?:this job is no longer active|position has been filled|listing has expired|jobopslag er ikke længere aktivt|jobbet besat|opslaget er udløbet|stillingen er ikke længere aktiv)\b/i.test(
    text
  );
}
