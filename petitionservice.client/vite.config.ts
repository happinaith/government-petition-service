import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vitest/config';
import plugin from '@vitejs/plugin-react';
import fs from 'fs';
import path from 'path';
import { env } from 'process';

const target = env["services__petitionservice-server__https__0"] ?? 'https://localhost:7174';

// https://vitejs.dev/config/
export default defineConfig(({ command }) => {
    const config = {
        plugins: [plugin()],
        resolve: {
            alias: {
                '@': fileURLToPath(new URL('./src', import.meta.url))
            }
        },
        test: {
            environment: 'jsdom',
            setupFiles: ['./src/test/setup.ts'],
            css: true,
            clearMocks: true,
            restoreMocks: true,
        },
    };

    if (command !== 'serve') {
        return config;
    }

    const baseFolder =
        env.APPDATA !== undefined && env.APPDATA !== ''
            ? `${env.APPDATA}/ASP.NET/https`
            : `${env.HOME}/.aspnet/https`;

    const certificateName = "petitionservice.client";
    const certFilePath = path.join(baseFolder, `${certificateName}.pem`);
    const keyFilePath = path.join(baseFolder, `${certificateName}.key`);

    if (!fs.existsSync(baseFolder)) {
        fs.mkdirSync(baseFolder, { recursive: true });
    }

    if (!fs.existsSync(certFilePath) || !fs.existsSync(keyFilePath)) {
        throw new Error("HTTPS certificate is missing. Run the app in development once to generate it.");
    }

    return {
        ...config,
        server: {
            proxy: {
                '^/weatherforecast': {
                    target,
                    secure: false
                },
                '^/api': { target, secure: false }
            },
            port: parseInt(env.DEV_SERVER_PORT || '51892'),
            https: {
                key: fs.readFileSync(keyFilePath),
                cert: fs.readFileSync(certFilePath),
            }
        }
    };
})
