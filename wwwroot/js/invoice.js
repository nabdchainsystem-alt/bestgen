document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll("[data-invoice-form]").forEach((form) => {
        const body = form.querySelector("[data-items-body]");
        const template = form.querySelector("[data-row-template]");
        const mode = form.dataset.mode || "sales";
        const unitField = mode === "purchase" ? "UnitCost" : "UnitPrice";

        const parseAmount = (value) => {
            const normalized = String(value || "0").replace(",", ".");
            const amount = Number.parseFloat(normalized);
            return Number.isFinite(amount) ? amount : 0;
        };

        const setTotal = (key, value) => {
            const display = form.querySelector(`[data-total="${key}"]`);
            const hidden = form.querySelector(`[data-hidden-total="${key}"]`);
            const fixed = value.toFixed(2);
            if (display) {
                display.textContent = fixed;
            }
            if (hidden) {
                hidden.value = fixed;
            }
        };

        const reindexRows = () => {
            body.querySelectorAll("[data-item-row]").forEach((row, index) => {
                row.querySelectorAll("[name]").forEach((input) => {
                    input.name = input.name.replace(/Items\[(?:\d+|__index__)\]/, `Items[${index}]`);
                });
            });
        };

        const calculateRow = (row) => {
            const qty = parseAmount(row.querySelector(".js-qty")?.value);
            const unit = parseAmount(row.querySelector(".js-unit")?.value);
            const discount = parseAmount(row.querySelector(".js-discount")?.value);
            const vatRate = parseAmount(row.querySelector(".js-vat")?.value);
            const taxable = Math.max(0, qty * unit - discount);
            const vat = taxable * vatRate / 100;
            const total = taxable + vat;
            const lineTotal = row.querySelector(".js-line-total");
            if (lineTotal) {
                lineTotal.value = total.toFixed(2);
            }
            return { subtotal: qty * unit, discount, vat, total };
        };

        const calculateInvoice = () => {
            let subtotal = 0;
            let discount = 0;
            let vat = 0;
            let grand = 0;

            body.querySelectorAll("[data-item-row]").forEach((row) => {
                const rowTotals = calculateRow(row);
                subtotal += rowTotals.subtotal;
                discount += rowTotals.discount;
                vat += rowTotals.vat;
                grand += rowTotals.total;
            });

            const paid = parseAmount(form.querySelector(".js-paid")?.value);
            setTotal("subtotal", subtotal);
            setTotal("discount", discount);
            setTotal("vat", vat);
            setTotal("grand", grand);
            setTotal("remaining", Math.max(0, grand - paid));
        };

        const applyProductDefaults = (select) => {
            const option = select.selectedOptions[0];
            const row = select.closest("[data-item-row]");
            if (!option || !row) {
                return;
            }

            const price = mode === "purchase" ? option.dataset.purchasePrice : option.dataset.sellingPrice;
            const unit = row.querySelector(".js-unit");
            const vat = row.querySelector(".js-vat");

            if (unit && price) {
                unit.value = price;
                unit.name = unit.name.replace(/Items\[\d+\]\.\w+/, (match) => match.replace(/\.\w+$/, `.${unitField}`));
            }

            if (vat && option.dataset.vat) {
                vat.value = option.dataset.vat;
            }

            calculateInvoice();
        };

        form.addEventListener("input", (event) => {
            if (event.target.matches(".js-qty, .js-unit, .js-discount, .js-vat, .js-paid")) {
                calculateInvoice();
            }
        });

        form.addEventListener("change", (event) => {
            if (event.target.matches(".js-product-select")) {
                applyProductDefaults(event.target);
            }
        });

        form.querySelector("[data-add-row]")?.addEventListener("click", () => {
            const index = body.querySelectorAll("[data-item-row]").length;
            const html = template.innerHTML.replaceAll("__index__", String(index));
            body.insertAdjacentHTML("beforeend", html);
            reindexRows();
            calculateInvoice();
        });

        form.addEventListener("click", (event) => {
            const button = event.target.closest(".js-remove-row");
            if (!button) {
                return;
            }

            const rows = body.querySelectorAll("[data-item-row]");
            if (rows.length === 1) {
                rows[0].querySelectorAll("input").forEach((input) => {
                    input.value = input.classList.contains("js-qty") ? "1" : "0";
                });
                rows[0].querySelector(".js-product-select").value = "";
            } else {
                button.closest("[data-item-row]").remove();
            }

            reindexRows();
            calculateInvoice();
        });

        reindexRows();
        calculateInvoice();
    });
});
