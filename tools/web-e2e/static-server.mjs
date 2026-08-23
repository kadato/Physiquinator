// Minimal static file server for local verification of published Blazor output.
// Usage: node static-server.mjs <rootDir> <port>
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { extname, join, normalize, resolve } from 'node:path';

const root = process.argv[2] ?? '.';
const port = Number(process.argv[3] ?? 8080);
const absoluteRoot = resolve(root);

const mime = {
    '.html': 'text/html; charset=utf-8',
    '.js': 'text/javascript',
    '.mjs': 'text/javascript',
    '.css': 'text/css',
    '.json': 'application/json',
    '.wasm': 'application/wasm',
    '.dat': 'application/octet-stream',
    '.dll': 'application/octet-stream',
    '.pdb': 'application/octet-stream',
    '.png': 'image/png',
    '.svg': 'image/svg+xml',
    '.woff': 'font/woff',
    '.woff2': 'font/woff2',
    '.ttf': 'font/ttf',
};

const server = createServer(async (req, res) => {
    try {
        const url = new URL(req.url ?? '/', `http://${req.headers.host}`);
        let rel = decodeURIComponent(url.pathname);
        if (rel.endsWith('/')) {
            rel += 'index.html';
        }
        // SPA fallback for client-side routes
        const candidates = [normalize(join(absoluteRoot, rel)), normalize(join(absoluteRoot, 'index.html'))];
        for (const candidate of candidates) {
            if (!candidate.startsWith(absoluteRoot)) {
                continue;
            }
            try {
                const data = await readFile(candidate);
                res.writeHead(200, {
                    'Content-Type': mime[extname(candidate).toLowerCase()] ?? 'application/octet-stream',
                    'Cache-Control': 'no-cache',
                });
                res.end(data);
                return;
            } catch {
                continue;
            }
        }
        res.writeHead(404);
        res.end('not found');
    } catch (err) {
        res.writeHead(500);
        res.end(String(err));
    }
});

server.listen(port, () => console.log(`serving ${root} on http://localhost:${port}/`));
