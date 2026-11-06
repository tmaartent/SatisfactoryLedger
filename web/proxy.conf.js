// The dev server's proxy (ADR-0019): the app calls /api/..., and the dev server passes it on to the API.
// Under Aspire, the AppHost's WithReference(api) gives the API's address as services__api__https__0 (or http);
// started on its own, the dev server falls back to the address Aspire publishes the API on.
const target =
  process.env['services__api__https__0'] ?? process.env['services__api__http__0'] ?? 'https://localhost:7044';

module.exports = {
  '/api': {
    target,
    // The API's development certificate isn't trusted by Node.
    secure: false,
    changeOrigin: true,
  },
};
