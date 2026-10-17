// Publishes the .NET API as a self-contained single-file executable and places it where
// Tauri expects a sidecar: src-tauri/binaries/pharmaai-api-<target-triple>[.exe].
import { execFileSync } from "node:child_process";
import { copyFileSync, mkdirSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const RUNTIME_IDS = {
  "x86_64-pc-windows-msvc": "win-x64",
  "aarch64-pc-windows-msvc": "win-arm64",
  "x86_64-unknown-linux-gnu": "linux-x64",
  "aarch64-unknown-linux-gnu": "linux-arm64",
  "x86_64-apple-darwin": "osx-x64",
  "aarch64-apple-darwin": "osx-arm64",
};

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
const project = join(root, "backend", "src", "PharmaAI.Api", "PharmaAI.Api.csproj");
const publishDir = join(root, "backend", "artifacts", "sidecar");

const triple =
  process.env.TAURI_ENV_TARGET_TRIPLE ??
  execFileSync("rustc", ["-vV"], { encoding: "utf8" }).match(/^host: (\S+)$/m)?.[1];

const runtimeId = RUNTIME_IDS[triple];
if (!runtimeId) {
  console.error(`Unsupported target triple: ${triple}`);
  process.exit(1);
}

execFileSync(
  "dotnet",
  [
    "publish", project,
    "-c", "Release",
    "-r", runtimeId,
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-o", publishDir,
  ],
  { stdio: "inherit" },
);

const extension = runtimeId.startsWith("win") ? ".exe" : "";
const target = join(root, "frontend", "src-tauri", "binaries", `pharmaai-api-${triple}${extension}`);
mkdirSync(dirname(target), { recursive: true });
copyFileSync(join(publishDir, `PharmaAI.Api${extension}`), target);
console.log(`Sidecar ready: ${target}`);
