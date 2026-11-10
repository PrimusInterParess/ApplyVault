export const WORK_IN_DENMARK_PLACES = [
  { id: 'copenhagen', label: 'Copenhagen' },
  { id: 'aarhus', label: 'Aarhus' },
  { id: 'odense', label: 'Odense' },
  { id: 'aalborg', label: 'Aalborg' }
] as const;

export type WorkInDenmarkPlaceId = (typeof WORK_IN_DENMARK_PLACES)[number]['id'];

export function normalizeWorkInDenmarkPlace(value: string | null | undefined): WorkInDenmarkPlaceId | null {
  const normalized = value?.trim().toLowerCase();

  if (!normalized) {
    return null;
  }

  return WORK_IN_DENMARK_PLACES.find((place) => place.id === normalized)?.id ?? null;
}

export function workInDenmarkPlaceLabel(place: WorkInDenmarkPlaceId | null): string {
  if (!place) {
    return 'All of Denmark';
  }

  return WORK_IN_DENMARK_PLACES.find((candidate) => candidate.id === place)?.label ?? 'All of Denmark';
}
