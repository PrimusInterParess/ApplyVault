export function formatIndexedSearchSummary(
  loaded: number,
  indexedTotal: number,
  keywords: string,
  options?: {
    upstreamTotal?: number | null;
    resultsTruncated?: boolean;
    sourceLabel?: string;
  }
): string {
  const sourceLabel = options?.sourceLabel ?? 'source';
  const upstreamTotal = options?.upstreamTotal ?? null;
  const truncated = options?.resultsTruncated === true
    && upstreamTotal !== null
    && upstreamTotal > indexedTotal;

  const indexedLabel = truncated
    ? `${indexedTotal} indexed`
    : `${indexedTotal}`;

  if (loaded < indexedTotal) {
    const base = `Showing ${loaded} of ${indexedLabel} · ${keywords}`;
    return truncated ? `${base} (${upstreamTotal} on ${sourceLabel})` : base;
  }

  const base = `${indexedTotal} ${indexedTotal === 1 ? 'listing' : 'listings'} · ${keywords}`;
  return truncated ? `${base} (${upstreamTotal} on ${sourceLabel})` : base;
}
