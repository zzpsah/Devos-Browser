const statusEl = document.getElementById("status");
const protocolEl = document.getElementById("protocol");
const versionEl = document.getElementById("version");
const detailEl = document.getElementById("detail");
const reconnectButton = document.getElementById("reconnect");

async function refresh() {
  const manifest = chrome.runtime.getManifest();
  versionEl.textContent = manifest.version;

  const state = await chrome.runtime.sendMessage({ type: "devos.bridge.status" });
  statusEl.textContent = state?.devosBridgeState ?? "unknown";
  protocolEl.textContent = state?.devosBridgeProtocolVersion ?? "—";
  detailEl.textContent = state?.devosBridgeDetail ?? "";
}

reconnectButton.addEventListener("click", async () => {
  reconnectButton.disabled = true;
  await chrome.runtime.sendMessage({ type: "devos.bridge.reconnect" });
  setTimeout(async () => {
    await refresh();
    reconnectButton.disabled = false;
  }, 500);
});

refresh();
