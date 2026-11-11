// How the app shows what the API returns: game class names as words, centimetres as metres, rates per minute.

/** A position in the game's units (centimetres), as the API returns it. */
export interface Position {
  x: number;
  y: number;
  z: number;
}

/** "Build_ConstructorMk1_C" → "Constructor Mk1": for anything the catalog has no name for. */
export function classNameAsWords(className: string): string {
  return className
    .replace(/^(Build|Desc|Recipe|BP)_/, '')
    .replace(/_C$/, '')
    .replace(/_/g, ' ')
    .replace(/([a-z])([A-Z0-9])/g, '$1 $2')
    .replace(/([A-Z])([A-Z][a-z])/g, '$1 $2')
    .replace(/(\d)\s+(?=\d)/g, '$1')
    .replace(/Mk (\d)/g, 'Mk$1');
}

/** Metres, rounded, as the game's map shows them. */
export function positionInMetres(position: Position | null | undefined): string {
  if (!position) return '—';
  const metres = (centimetres: number) => String(Math.round(centimetres / 100)).replace('-', '−');
  return `${metres(position.x)}, ${metres(position.y)}, ${metres(position.z)} m`;
}

/**
 * Utilisation (0 to 1) as a percentage: whole, unless that would round a machine below 100% up to 100%.
 * Then one decimal, rounded down, so 99.84% shows as 99.8%.
 */
export function percent(fraction: number): string {
  const whole = Math.round(fraction * 100);
  if (whole < 100 || fraction >= 1) return `${whole}%`;
  return `${Math.floor(fraction * 1000) / 10}%`;
}

/** Power in MW, with at most one decimal and the thousands grouped: a power plant makes thousands. */
export function megawatts(power: number): string {
  return `${power.toLocaleString('en-US', { maximumFractionDigits: 1 })} MW`;
}

/** A rate per minute, with at most two decimals. */
export function perMinute(rate: number): string {
  return `${Number(rate.toFixed(2))}/min`;
}
