// Aspire injects service discovery URLs into the Angular process.
const target = process.env.services__api__http__0 || 'http://localhost:5100';
module.exports = {
  '/api': { target, secure: false, changeOrigin: true },
  '/health': { target, secure: false, changeOrigin: true },
};
