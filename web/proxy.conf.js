// The dev server's proxy (ADR-0019): the app calls /api/..., and the dev server passes it on to the API.
// Under Aspire, the AppHost's WithReference(api) gives the API's address as services__api__https__0 (or http);
// started on its own, the dev server falls back to the address Aspire publishes the API on.
const target =
  process.env['services__api__https__0'] ??
  process.env['services__api__http__0'] ??
  'https://localhost:7044';

// The Assistant (ADR-0030) answers /api/v1/assistant/... It comes first: the first matching path wins.
const assistant =
  process.env['services__assistant__https__0'] ??
  process.env['services__assistant__http__0'] ??
  'http://localhost:5290';

module.exports = {
  '/api/v1/assistant': {
    target: assistant,
    secure: false,
    changeOrigin: true,
  },
  '/api': {
    target,
    // The API's development certificate isn't trusted by Node.
    secure: false,
    changeOrigin: true,
  },
};
