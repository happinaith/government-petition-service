import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vitest/config';
import plugin from '@vitejs/plugin-react';
import child_process from 'child_process';
import fs from 'fs';
import path from 'path';
import { env } from 'process';

const target = env["services__petitionservice-server__https__0"] ?? 'https://localhost:7174';
const isVitestRun = process.argv.some((argument) => argument.includes('vitest'));

function ensureDevelopmentCertificate() {
    const baseFolder =
        env.APPDATA !== undefined && env.APPDATA !== ''
            ? `${env.APPDATA}/ASP.NET/https`
            : `${env.HOME}/.aspnet/https`;

    const certificateName = 'petitionservice.client';
    const certFilePath = path.join(baseFolder, `${certificateName}.pem`);
    const keyFilePath = path.join(baseFolder, `${certificateName}.key`);

    if (!fs.existsSync(baseFolder)) {
        fs.mkdirSync(baseFolder, { recursive: true });
    }

    if (!fs.existsSync(certFilePath) || !fs.existsSync(keyFilePath)) {
        if (0 !== child_process.spawnSync('dotnet', [
            'dev-certs',
            'https',
            '--export-path',
            certFilePath,
            '--format',
            'Pem',
            '--no-password',
        ], { stdio: 'inherit' }).status) {
            throw new Error('Could not create certificate.');
        }
    }

    return {
        certFilePath,
        keyFilePath,
    };
}

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

    if (command !== 'serve' || isVitestRun) {
        return config;
    }

    const { certFilePath, keyFilePath } = ensureDevelopmentCertificate();

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
