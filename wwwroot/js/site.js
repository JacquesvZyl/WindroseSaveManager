const loadingOverlay = document.querySelector("[data-loading-overlay]");
const loadingMessage = document.querySelector("[data-loading-message]");
const loadingTitle = document.querySelector("[data-loading-title]");

document.querySelectorAll("form[method='post']").forEach((form) => {
  form.addEventListener("submit", () => {
    if (!loadingOverlay) {
      return;
    }

    const submitter = document.activeElement;
    const message = form.dataset.loadingMessage || "Working on your request...";

    if (loadingTitle) {
      loadingTitle.textContent = submitter?.textContent?.trim() || "Working...";
    }

    if (loadingMessage) {
      loadingMessage.textContent = message;
    }

    loadingOverlay.hidden = false;
    document.body.classList.add("is-loading");

    form.querySelectorAll("button").forEach((button) => {
      button.disabled = true;
    });
  });
});
