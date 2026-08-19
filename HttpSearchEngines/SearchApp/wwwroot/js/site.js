const body = document.body;
const buttons = document.querySelectorAll("[data-tab-target]");
const panels = document.querySelectorAll("[data-tab-panel]");

function activateTab(tabName) {
    buttons.forEach(button => {
        const isActive = button.dataset.tabTarget === tabName;
        button.classList.toggle("active", isActive);
        button.setAttribute("aria-selected", isActive.toString());
    });

    panels.forEach(panel => {
        panel.classList.toggle("active", panel.dataset.tabPanel === tabName);
    });
}

buttons.forEach(button => {
    button.addEventListener("click", () => activateTab(button.dataset.tabTarget));
});

activateTab(body.dataset.activeTab || "text");
