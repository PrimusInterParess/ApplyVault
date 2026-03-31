import { build, context } from 'esbuild';
import { cp, mkdir, rm } from 'node:fs/promises';
import path from 'node:path';
import process from 'node:process';

const rootDir = process.cwd();
const distDir = path.resolve(rootDir, 'dist');
const isWatchMode = process.argv.includes('--watch');

const entryPoints = {
  'background/background': path.resolve(rootDir, 'src/background/background.ts'),
  'content/content': path.resolve(rootDir, 'src/content/content.ts'),
  'popup/popup': path.resolve(rootDir, 'src/popup/popup.ts')
};

const buildOptions = {
  entryPoints,
  outdir: distDir,
  bundle: true,
  format: 'iife',
  platform: 'browser',
  target: ['chrome120'],
  sourcemap: true,
  logLevel: 'info'
};

async function copyStaticAssets() {
  await Promise.all([
    mkdir(path.join(distDir, 'assets'), { recursive: true }),
    mkdir(path.join(distDir, 'popup'), { recursive: true }),
    cp(path.resolve(rootDir, 'manifest.json'), path.join(distDir, 'manifest.json')),
    cp(path.resolve(rootDir, 'src/assets/applyvault-icon.png'), path.join(distDir, 'assets/applyvault-icon.png')),
    cp(path.resolve(rootDir, 'src/popup/popup.html'), path.join(distDir, 'popup/popup.html')),
    cp(path.resolve(rootDir, 'src/popup/popup.css'), path.join(distDir, 'popup/popup.css'))
  ]);
}

async function prepareDistDirectory() {
  await rm(distDir, { recursive: true, force: true });
  await mkdir(distDir, { recursive: true });
}

async function runBuild() {
  await prepareDistDirectory();
  await build(buildOptions);
  await copyStaticAssets();
}

if (isWatchMode) {
  await prepareDistDirectory();
  const buildContext = await context({
    ...buildOptions,
    plugins: [
      {
        name: 'copy-static-assets',
        setup(currentBuild) {
          currentBuild.onEnd(async (result) => {
            if (result.errors.length === 0) {
              await copyStaticAssets();
            }
          });
        }
      }
    ]
  });

  await buildContext.watch();
  await copyStaticAssets();
  console.log('Watching extension sources...');
} else {
  await runBuild();
}
