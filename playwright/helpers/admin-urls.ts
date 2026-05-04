export const ADMIN_BASE = '/admin';

export const adminUrls = {
  home: () => `${ADMIN_BASE}/`,
  login: () => `${ADMIN_BASE}/login/`,
  loginNext: (next: string) => `${ADMIN_BASE}/login/?next=${encodeURIComponent(next)}`,
  logout: () => `${ADMIN_BASE}/logout/`,
  list: (entity: string) => `${ADMIN_BASE}/${entity}/`,
  add: (entity: string) => `${ADMIN_BASE}/${entity}/add/`,
  change: (entity: string, id: string | number) => `${ADMIN_BASE}/${entity}/${id}/change/`,
  delete: (entity: string, id: string | number) => `${ADMIN_BASE}/${entity}/${id}/delete/`,
  bulkAction: (entity: string) => `${ADMIN_BASE}/${entity}/action/`,
  bulkDelete: (entity: string) => `${ADMIN_BASE}/${entity}/action/delete/`,
  popup: (entity: string, params: Record<string, string> = {}) => {
    const search = new URLSearchParams({ _popup: '1', ...params });
    return `${ADMIN_BASE}/${entity}/?${search.toString()}`;
  },
};

export const ADMIN_USERNAME = process.env.NDJANGO_ADMIN_USER ?? 'admin';
export const ADMIN_PASSWORD = process.env.NDJANGO_ADMIN_PASSWORD ?? 'admin';
