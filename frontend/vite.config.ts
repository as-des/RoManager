import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { fileURLToPath, URL } from 'node:url'

const fromRoot = (p: string) => fileURLToPath(new URL(p, import.meta.url))

// Production: сборка кладётся прямо в wwwroot (legacy сохранён в wwwroot_legacy).
export default defineConfig({
  plugins: [react()],
  base: '/',
  build: {
    outDir: fromRoot('../wwwroot'),
    emptyOutDir: true,
    rollupOptions: {
      input: {
        main: fromRoot('index.html'),
        settings: fromRoot('settings.html'),
      },
    },
  },
})
