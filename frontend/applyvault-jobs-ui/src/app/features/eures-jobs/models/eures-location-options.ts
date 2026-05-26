export type EuresLocationOption = {
  readonly code: string;
  readonly label: string;
};

export const EURES_LOCATION_OPTIONS: readonly EuresLocationOption[] = [
  { code: 'dk', label: 'Denmark' },
  { code: 'se', label: 'Sweden' },
  { code: 'no', label: 'Norway' },
  { code: 'fi', label: 'Finland' },
  { code: 'de', label: 'Germany' },
  { code: 'nl', label: 'Netherlands' },
  { code: 'be', label: 'Belgium' },
  { code: 'fr', label: 'France' },
  { code: 'es', label: 'Spain' },
  { code: 'it', label: 'Italy' },
  { code: 'pl', label: 'Poland' },
  { code: 'at', label: 'Austria' },
  { code: 'ie', label: 'Ireland' },
  { code: 'pt', label: 'Portugal' },
  { code: 'cz', label: 'Czechia' }
];

export const EURES_DEFAULT_LOCATION_CODE = 'dk';

export const EURES_LOCATION_CODES = new Set(
  EURES_LOCATION_OPTIONS.map((option) => option.code)
);

export function normalizeEuresLocationCode(code: string): string {
  return code.trim().toLowerCase();
}

export function isKnownEuresLocationCode(code: string): boolean {
  return EURES_LOCATION_CODES.has(normalizeEuresLocationCode(code));
}
