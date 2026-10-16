import { defineConfig } from 'vite';
export default defineConfig({ root: 'ui', clearScreen: false, build: { outDir: '../web-dist', emptyOutDir: true }, server: { strictPort: true, port: 1420 } });
