document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll("[data-journal-form]").forEach((form) => {
        const body = form.querySelector("[data-journal-lines]");
        const template = form.querySelector("[data-journal-template]");

        const parseAmount = (value) => {
            const amount = Number.parseFloat(String(value || "0").replace(",", "."));
            return Number.isFinite(amount) ? amount : 0;
        };

        const setTotal = (key, value) => {
            const display = form.querySelector(`[data-journal-total="${key}"]`);
            const hidden = form.querySelector(`[data-hidden-journal-total="${key}"]`);
            const fixed = value.toFixed(2);
            if (display) {
                display.textContent = fixed;
            }
            if (hidden) {
                hidden.value = fixed;
            }
        };

        const reindex = () => {
            body.querySelectorAll("[data-journal-row]").forEach((row, index) => {
                row.querySelectorAll("[name]").forEach((input) => {
                    input.name = input.name.replace(/Lines\[(?:\d+|__index__)\]/, `Lines[${index}]`);
                });
            });
        };

        const calculate = () => {
            let debit = 0;
            let credit = 0;
            body.querySelectorAll("[data-journal-row]").forEach((row) => {
                debit += parseAmount(row.querySelector(".js-debit")?.value);
                credit += parseAmount(row.querySelector(".js-credit")?.value);
            });
            setTotal("debit", debit);
            setTotal("credit", credit);
            setTotal("difference", debit - credit);
        };

        form.addEventListener("input", (event) => {
            if (event.target.matches(".js-debit, .js-credit")) {
                calculate();
            }
        });

        form.querySelector("[data-add-journal-row]")?.addEventListener("click", () => {
            const index = body.querySelectorAll("[data-journal-row]").length;
            body.insertAdjacentHTML("beforeend", template.innerHTML.replaceAll("__index__", String(index)));
            reindex();
            calculate();
        });

        form.addEventListener("click", (event) => {
            const button = event.target.closest(".js-remove-journal-row");
            if (!button) {
                return;
            }

            if (body.querySelectorAll("[data-journal-row]").length > 2) {
                button.closest("[data-journal-row]").remove();
            }
            reindex();
            calculate();
        });

        reindex();
        calculate();
    });
});
