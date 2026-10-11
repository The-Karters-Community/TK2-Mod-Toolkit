/** Game API signatures and local Ghidra pseudocode explorer. */
export function createGameApiBrowser({
  $,
  api,
  el,
  notice,
  openFunctionSource,
}) {
  let nextFunctions = null,
    functionSequence = 0,
    functionLoading = false,
    detailSequence = 0;
  let nextPseudocode = null,
    pseudocodeSequence = 0,
    pseudocodeLoading = false,
    pseudocodeDetailSequence = 0;

  async function searchFunctions(more = false) {
    if (more && (functionLoading || nextFunctions == null)) return;
    const sequence = ++functionSequence,
      offset = more ? nextFunctions : 0;
    functionLoading = true;
    $("function-loading").hidden = false;
    $("function-end").hidden = true;
    $("function-scroll").setAttribute("aria-busy", "true");
    if (!more) {
      nextFunctions = null;
      $("function-list").replaceChildren();
      $("function-scroll").scrollTop = 0;
      $("function-count").textContent = "Loading functions…";
      ++detailSequence;
      $("function-name").textContent = "Choose a function";
      $("function-owner").textContent = "";
      $("function-status").textContent =
        "Select a method name to inspect its API.";
      $("function-detail").textContent = "The signature appears here.";
      $("open-function-source").hidden = true;
      $("function-example").hidden = true;
      $("function-guidance").textContent = "";
    }
    try {
      const result = await api(
        "functions?q=" +
          encodeURIComponent($("function-query").value) +
          "&offset=" +
          offset +
          "&recovered=" +
          $("functions-recovered").checked +
          "&topic=" +
          $("function-topic").value,
      );
      if (sequence !== functionSequence) return;
      $("function-count").textContent =
        result.message ||
        `${result.total.toLocaleString()} results · ${result.indexed.toLocaleString()} indexed declarations · ${result.reconstructed} reconstructed implementations`;
      (result.methods || []).forEach((method) => {
        const name =
          method.name || method.signature.split("(")[0].split(" ").at(-1);
        const button = el("button", "function-row");
        button.dataset.function = method.id;
        button.append(
          el("strong", "", method.label || name),
          el("small", "", method.type),
          el(
            "span",
            "api-badge",
            method.source ? "Reconstructed C#" : "API declaration",
          ),
        );
        button.setAttribute("aria-pressed", "false");
        button.onclick = () => selectFunction(method);
        $("function-list").append(button);
      });
      nextFunctions = result.next;
      $("function-end").hidden = nextFunctions != null;
      $("function-end").textContent = result.total
        ? "End of results"
        : "No functions match. Try another category or All indexed functions.";
    } catch (error) {
      if (sequence === functionSequence) {
        $("function-end").hidden = false;
        $("function-end").textContent =
          "Could not load functions. Choose Search to retry.";
        notice(error.message, true);
      }
    } finally {
      if (sequence === functionSequence) {
        functionLoading = false;
        $("function-loading").hidden = true;
        $("function-scroll").setAttribute("aria-busy", "false");
      }
    }
  }

  async function selectFunction(method) {
    const sequence = ++detailSequence;
    document.querySelectorAll("[data-function]").forEach((button) => {
      const active = button.dataset.function === method.id;
      button.classList.toggle("selected", active);
      button.setAttribute("aria-pressed", String(active));
    });
    $("function-name").textContent = method.label || method.name || method.type;
    $("function-owner").textContent = method.type;
    $("function-status").textContent = "Loading reference…";
    $("open-function-source").hidden = true;
    $("function-example").hidden = true;
    try {
      const detail = await api("function?id=" + encodeURIComponent(method.id));
      if (sequence !== detailSequence) return;
      $("function-name").textContent =
        detail.label || method.name || method.type;
      $("function-owner").textContent = detail.type;
      $("function-status").textContent =
        detail.status + (detail.confidence ? ". " + detail.confidence : ".");
      $("function-detail").textContent = detail.declaration;
      $("function-guidance").textContent =
        detail.access === "private" || detail.access === "internal"
          ? `${detail.access} game method: inspect existing adapters or a complete-signature Harmony hook before using it. This declaration has no editable body.`
          : "This signature lists the return type and parameters. A declaration alone does not prove runtime behavior.";
      $("open-function-source").hidden = !detail.source;
      $("open-function-source").onclick = () =>
        detail.source && openFunctionSource(detail.source);
      const usage = {
        "PixelKartPhysics.AddVelocity":
          "ReadableGame.AddVelocity(kart, Vector3.up * _hop.Value);",
        "PixelKartPhysics.JumpInput": "ReadableGame.JumpInput(kart, true);",
        "PixelGameKartCamera.GetCamera":
          "var gameCamera = ReadableGame.GetCamera(camera);",
        "PixelGameKartCamera.IsCameraASpectatorCamera":
          "bool spectator = ReadableGame.IsSpectator(camera);",
      }[detail.type + "." + detail.name];
      $("function-example").hidden = !usage;
      $("function-usage").textContent = usage || "";
    } catch (error) {
      if (sequence === detailSequence) {
        $("function-status").textContent =
          "Reference unavailable. Select the function to retry.";
        notice(error.message, true);
      }
    }
  }

  async function searchPseudocode(more = false) {
    if (more && (pseudocodeLoading || nextPseudocode == null)) return;
    const sequence = ++pseudocodeSequence,
      offset = more ? nextPseudocode : 0;
    pseudocodeLoading = true;
    $("pseudocode-more").disabled = true;
    if (!more) {
      nextPseudocode = null;
      $("pseudocode-list").replaceChildren();
      $("pseudocode-more").hidden = true;
      $("pseudocode-count").textContent = "Searching local Ghidra pseudocode…";
      ++pseudocodeDetailSequence;
      $("pseudocode-name").textContent = "Choose a result";
      $("pseudocode-address").textContent = "";
      $("pseudocode-viewer").textContent =
        "Select a match to inspect its native pseudocode.";
    }
    try {
      const result = await api(
        "pseudocode?q=" +
          encodeURIComponent($("pseudocode-query").value) +
          "&offset=" +
          offset,
      );
      if (sequence !== pseudocodeSequence) return;
      $("pseudocode-count").textContent =
        `${result.total.toLocaleString()} matches across ${result.indexed.toLocaleString()} local exports. ${result.message || "Ghidra native pseudocode."}`;
      (result.results || []).forEach((item) => {
        const button = el("button", "pseudocode-row");
        button.dataset.pseudocode = item.id;
        button.append(
          el("strong", "", item.name),
          el("small", "", item.address),
          el("span", "pseudocode-snippet", item.snippet),
        );
        button.setAttribute("aria-pressed", "false");
        button.onclick = () => openPseudocode(item);
        $("pseudocode-list").append(button);
      });
      nextPseudocode = result.next;
      $("pseudocode-more").hidden = nextPseudocode == null;
    } catch (error) {
      if (sequence === pseudocodeSequence) {
        $("pseudocode-count").textContent =
          "Could not search local pseudocode. Choose Search to retry.";
        notice(error.message, true);
      }
    } finally {
      if (sequence === pseudocodeSequence) {
        pseudocodeLoading = false;
        $("pseudocode-more").disabled = false;
      }
    }
  }

  async function openPseudocode(item) {
    const sequence = ++pseudocodeDetailSequence;
    document.querySelectorAll("[data-pseudocode]").forEach((button) => {
      const active = button.dataset.pseudocode === item.id;
      button.classList.toggle("selected", active);
      button.setAttribute("aria-pressed", String(active));
    });
    $("pseudocode-name").textContent = item.name;
    $("pseudocode-address").textContent = "VA 0x" + item.address;
    $("pseudocode-viewer").textContent = "Loading native pseudocode…";
    try {
      const result = await api("pseudocode?id=" + encodeURIComponent(item.id));
      if (sequence === pseudocodeDetailSequence)
        $("pseudocode-viewer").textContent =
          result.code + (result.truncated ? "\n\n" + result.message : "");
    } catch (error) {
      if (sequence === pseudocodeDetailSequence) {
        $("pseudocode-viewer").textContent =
          "Could not open this pseudocode file.";
        notice(error.message, true);
      }
    }
  }

  $("function-topic").value = "important";
  $("function-search").onclick = () =>
    searchFunctions().catch((error) => notice(error.message, true));
  $("function-query").onkeydown = (event) => {
    if (event.key === "Enter") $("function-search").click();
  };
  $("function-query").oninput = () => {
    clearTimeout(searchFunctions.timer);
    searchFunctions.timer = setTimeout(() => $("function-search").click(), 300);
  };
  $("function-topic").onchange = () => $("function-search").click();
  $("functions-recovered").onchange = () => $("function-search").click();
  $("function-scroll").onscroll = () => {
    const list = $("function-scroll");
    if (list.scrollHeight - list.scrollTop - list.clientHeight < 180)
      searchFunctions(true).catch((error) => notice(error.message, true));
  };
  $("pseudocode-search").onclick = () =>
    searchPseudocode().catch((error) => notice(error.message, true));
  $("pseudocode-query").onkeydown = (event) => {
    if (event.key === "Enter") $("pseudocode-search").click();
  };
  $("pseudocode-more").onclick = () =>
    searchPseudocode(true).catch((error) => notice(error.message, true));
  return { searchFunctions, searchPseudocode };
}
