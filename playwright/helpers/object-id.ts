/**
 * Utilities for working with MongoDB ObjectId values in URLs and form payloads.
 * The dashboard renders an ObjectId as its 24-character lowercase hex string.
 */

export const OBJECT_ID_PATTERN = /[0-9a-f]{24}/;
export const OBJECT_ID_PATTERN_ANCHORED = /^[0-9a-f]{24}$/;

export function isObjectId(value: string): boolean {
  return OBJECT_ID_PATTERN_ANCHORED.test(value);
}

/**
 * Generates a syntactically-valid 24-char hex ObjectId. The bytes do not need
 * to match a real Mongo document — used only when a test wants to negative-test
 * the dashboard's response to a non-existent reference.
 */
export function fakeObjectId(): string {
  const bytes = Array.from({ length: 12 }, () => Math.floor(Math.random() * 256));
  return bytes.map((b) => b.toString(16).padStart(2, '0')).join('');
}

/**
 * Extracts the ObjectId from a Mongo dashboard URL of the form
 * `/admin/{Entity}/{objectId}/{change|delete|...}/`.
 */
export function extractObjectIdFromUrl(url: string, entity: string): string {
  const re = new RegExp(`/admin/${entity}/(${OBJECT_ID_PATTERN.source})/`);
  const match = url.match(re);
  if (!match) {
    throw new Error(`Failed to extract ObjectId for "${entity}" from URL: ${url}`);
  }
  return match[1];
}
