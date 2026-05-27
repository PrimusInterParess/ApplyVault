import { build, context } from 'esbuild';
import { cp, mkdir, mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import process from 'node:process';
import { pathToFileURL } from 'node:url';

const rootDir = process.cwd();
const distDir = path.resolve(rootDir, 'dist');
const isWatchMode = process.argv.includes('--watch');

const VALID_CONFIGURATIONS = ['development', 'staging', 'production'];

function parseConfiguration() {
  const configIndex = process.argv.indexOf('--configuration');

  if (configIndex === -1 || configIndex === process.argv.length - 1) {
    return 'development';
  }

  const configuration = process.argv[configIndex + 1];

  if (!VALID_CONFIGURATIONS.includes(configuration)) {
    throw new Error(
      `Unknown configuration "${configuration}". Use one of: ${VALID_CONFIGURATIONS.join(', ')}.`
    );
  }

  return configuration;
}

function environmentFileFor(configuration) {
  return configuration === 'development'
    ? 'environment.ts'
    : `environment.${configuration}.ts`;
}

function manifestFileFor(configuration) {
  return configuration === 'development'
    ? 'manifest.json'
    : `manifest.${configuration}.json`;
}

const configuration = parseConfiguration();
const environmentFile = environmentFileFor(configuration);
const manifestFile = manifestFileFor(configuration);
const resolvedEnvironmentPath = path.resolve(rootDir, 'src/environments', environmentFile);
const resolvedManifestPath = path.resolve(rootDir, manifestFile);

const entryPoints = {
  'background/background': path.resolve(rootDir, 'src/background/background.ts'),
  'content/content': path.resolve(rootDir, 'src/content/content.ts'),
  'popup/popup': path.resolve(rootDir, 'src/popup/popup.ts')
};

async function validateEnvironmentFile() {
  const tempDir = await mkdtemp(path.join(tmpdir(), 'applyvault-env-'));
  const compiledEnvironmentPath = path.join(tempDir, 'environment.mjs');

  try {
    await build({
      entryPoints: [resolvedEnvironmentPath],
      outfile: compiledEnvironmentPath,
      bundle: true,
      platform: 'node',
      format: 'esm',
      logLevel: 'silent'
    });

    const { environment } = await import(pathToFileURL(compiledEnvironmentPath).href);

    if (!environment?.apiBaseUrl?.trim()) {
      throw new Error(
        `Extension build (${configuration}): apiBaseUrl is empty in src/environments/${environmentFile}.`
      );
    }

    if (!environment?.supabase?.url?.trim()) {
      throw new Error(
        `Extension build (${configuration}): supabase.url is empty in src/environments/${environmentFile}.`
      );
    }

    if (!environment?.supabase?.anonKey?.trim()) {
      throw new Error(
        `Extension build (${configuration}): supabase.anonKey is empty in src/environments/${environmentFile}.`
      );
    }
  } finally {
    await rm(tempDir, { recursive: true, force: true });
  }
}

const environmentReplacementPlugin = {
  name: 'environment-replacement',
  setup(currentBuild) {
    currentBuild.onResolve({ filter: /environments\/environment$/ }, () => ({
      path: resolvedEnvironmentPath
    }));
  }
};

const buildOptions = {
  entryPoints,
  outdir: distDir,
  bundle: true,
  format: 'iife',
  platform: 'browser',
  target: ['chrome120'],
  sourcemap: true,
  logLevel: 'info',
  plugins: [environmentReplacementPlugin]
};

async function copyStaticAssets() {
  await Promise.all([
    mkdir(path.join(distDir, 'assets'), { recursive: true }),
    mkdir(path.join(distDir, 'popup'), { recursive: true }),
    cp(resolvedManifestPath, path.join(distDir, 'manifest.json')),
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
  console.log(`Building extension (${configuration})...`);
  console.log(`  environment: src/environments/${environmentFile}`);
  console.log(`  manifest: ${manifestFile}`);

  await validateEnvironmentFile();

  await prepareDistDirectory();
  await build(buildOptions);
  await copyStaticAssets();
}

if (isWatchMode) {
  await validateEnvironmentFile();
  await prepareDistDirectory();
  const buildContext = await context({
    ...buildOptions,
    plugins: [
      environmentReplacementPlugin,
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
  console.log(`Watching extension sources (${configuration})...`);
} else {
  await runBuild();
}
