// Owned JS module (design C7), scoped to NON-modal surfaces (DropdownMenu/Select content,
// PR6): capture-phase outside pointerdown + Escape, routed through the same
// [JSInvokable] RequestDismissAsync callback shape ui-modal.js uses for Dialog. Dialog does not
// use this module — showModal()'s native `cancel` event and document inertness already cover
// its dismissal and focus-containment needs (see ui-modal.js).

const activations = new Map();

export function activate(id, containerEl, dotNetRef) {
  function requestDismiss() {
    dotNetRef.invokeMethodAsync("RequestDismissAsync");
  }

  function onPointerDown(event) {
    if (!containerEl.contains(event.target)) {
      requestDismiss();
    }
  }

  function onKeyDown(event) {
    if (event.key === "Escape") {
      requestDismiss();
    }
  }

  document.addEventListener("pointerdown", onPointerDown, true);
  document.addEventListener("keydown", onKeyDown, true);
  activations.set(id, { onPointerDown, onKeyDown });
}

export function deactivate(id) {
  const handlers = activations.get(id);
  if (!handlers) {
    return;
  }

  document.removeEventListener("pointerdown", handlers.onPointerDown, true);
  document.removeEventListener("keydown", handlers.onKeyDown, true);
  activations.delete(id);
}
