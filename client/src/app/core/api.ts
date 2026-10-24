/**
 * The API's routes start with this prefix. The dev server (proxy.conf.json) and the demo host
 * (render.yaml) forward it unchanged to the API, so the client calls the API on its own origin.
 */
export const API_BASE_URL = '/api';

/** Health checks sit at the API's root, outside {@link API_BASE_URL}, and are forwarded the same way. */
export const HEALTH_URL = '/health';
