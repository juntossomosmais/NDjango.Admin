export function uniqueSuffix(): string {
  return `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`;
}

export function uniqueName(prefix: string): string {
  return `${prefix}-${uniqueSuffix()}`;
}
