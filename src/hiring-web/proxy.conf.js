// Aspire injects service discovery URLs into the Angular process.
const target = process.env.services__gateway__https__0 || 'https://localhost:7100';
module.exports = {
  '/api': { target, secure: false, changeOrigin: true },
  '/health': { target, secure: false, changeOrigin: true },
};
