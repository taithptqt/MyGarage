/**
 * Upload mọi file trong public/assets/booking lên Azure Blob, path blob: booking/<tên file>
 *
 * Cách dùng (PowerShell, từ thư mục frontend):
 *   $env:AZURE_STORAGE_CONNECTION_STRING = "<copy từ backend AzureStorage:ConnectionString>"
 *   npm run upload:booking-images
 *
 * Hoặc một dòng:
 *   AZURE_STORAGE_CONNECTION_STRING="..." npm run upload:booking-images
 */
import { BlobServiceClient } from "@azure/storage-blob";
import { readdir, stat } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const localDir = path.join(root, "public", "assets", "booking");

const connectionString = process.env.AZURE_STORAGE_CONNECTION_STRING?.trim();
const containerName = (process.env.AZURE_STORAGE_CONTAINER || "uploads").trim();

if (!connectionString) {
  console.error(
    "Thiếu AZURE_STORAGE_CONNECTION_STRING. Lấy giá trị giống AzureStorage:ConnectionString trong appsettings (backend)."
  );
  process.exit(1);
}

const imageTypes = {
  ".jpg": "image/jpeg",
  ".jpeg": "image/jpeg",
  ".png": "image/png",
  ".webp": "image/webp",
  ".gif": "image/gif",
  ".svg": "image/svg+xml",
};

async function main() {
  const dirStat = await stat(localDir).catch(() => null);
  if (!dirStat?.isDirectory()) {
    console.error("Không thấy thư mục:", localDir);
    process.exit(1);
  }

  const names = await readdir(localDir);
  const files = names.filter((n) => {
    const ext = path.extname(n).toLowerCase();
    return imageTypes[ext];
  });

  if (files.length === 0) {
    console.error("Không có file ảnh trong:", localDir);
    process.exit(1);
  }

  const service = BlobServiceClient.fromConnectionString(connectionString);
  const container = service.getContainerClient(containerName);
  await container.createIfNotExists({ access: "blob" });

  console.log(`Container: ${containerName}, prefix blob: booking/`);
  for (const name of files) {
    const ext = path.extname(name).toLowerCase();
    const contentType = imageTypes[ext] || "application/octet-stream";
    const blobPath = `booking/${name}`;
    const client = container.getBlockBlobClient(blobPath);
    const filePath = path.join(localDir, name);
    await client.uploadFile(filePath, {
      blobHTTPHeaders: { blobContentType: contentType },
    });
    console.log("OK", client.url);
  }

  const bookingBase = container.getBlockBlobClient(`booking/${files[0]}`).url.replace(/\/[^/]+$/, "");
  console.log("\nThêm vào frontend/.env rồi build lại:");
  console.log(`VITE_BOOKING_ASSETS_BASE=${bookingBase}`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
