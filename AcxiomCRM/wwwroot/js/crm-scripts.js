/**
 * AcxiomCRM Core UI & Client Validation Enhancements
 */
document.addEventListener("DOMContentLoaded", function () {
    // 1. Sidebar toggle
    const toggleBtn = document.getElementById("crmSidebarToggle");
    const sidebar = document.getElementById("crmSidebar");
    if (toggleBtn && sidebar) {
        toggleBtn.addEventListener("click", function () {
            sidebar.classList.toggle("collapsed");
        });
    }

    // 2. Client-side Form Validations
    const crmForms = document.querySelectorAll(".crm-validate-form");
    crmForms.forEach(function (form) {
        form.addEventListener("submit", function (e) {
            let hasError = false;

            // Opportunity Amount > 0 check
            const amountInput = form.querySelector("[name='Amount']");
            if (amountInput) {
                const val = parseFloat(amountInput.value);
                const errSpan = form.querySelector("[data-valmsg-for='Amount']");
                if (isNaN(val) || val <= 0) {
                    if (errSpan) errSpan.innerText = "Opportunity Amount must be greater than 0.";
                    hasError = true;
                }
            }

            // Probability 0 - 100 check
            const probInput = form.querySelector("[name='Probability']");
            if (probInput) {
                const val = parseInt(probInput.value, 10);
                const errSpan = form.querySelector("[data-valmsg-for='Probability']");
                if (isNaN(val) || val < 0 || val > 100) {
                    if (errSpan) errSpan.innerText = "Probability must be between 0 and 100.";
                    hasError = true;
                }
            }

            // Follow-up Date cannot be earlier than today check
            const followUpDateInput = form.querySelector("[name='FollowUpDate']");
            const statusInput = form.querySelector("[name='Status']");
            if (followUpDateInput && (!statusInput || statusInput.value === "Planned")) {
                const dateVal = new Date(followUpDateInput.value);
                const today = new Date();
                today.setHours(0, 0, 0, 0);

                const errSpan = form.querySelector("[data-valmsg-for='FollowUpDate']");
                if (dateVal < today) {
                    if (errSpan) errSpan.innerText = "Follow-up date cannot be earlier than today.";
                    hasError = true;
                }
            }

            // Expected Close Date check for active opportunities
            const closeDateInput = form.querySelector("[name='ExpectedCloseDate']");
            if (closeDateInput && (!statusInput || statusInput.value === "Open")) {
                const dateVal = new Date(closeDateInput.value);
                const today = new Date();
                today.setHours(0, 0, 0, 0);

                const errSpan = form.querySelector("[data-valmsg-for='ExpectedCloseDate']");
                if (dateVal < today) {
                    if (errSpan) errSpan.innerText = "Expected Close Date cannot be in the past.";
                    hasError = true;
                }
            }

            // Phone 10-digit pattern check
            const phoneInput = form.querySelector("[name='Phone']");
            if (phoneInput) {
                const phoneVal = phoneInput.value.trim();
                const phoneRegex = /^[0-9]{10}$/;
                const errSpan = form.querySelector("[data-valmsg-for='Phone']");
                if (!phoneRegex.test(phoneVal)) {
                    if (errSpan) errSpan.innerText = "Enter a valid 10-digit phone number.";
                    hasError = true;
                }
            }

            if (hasError) {
                e.preventDefault();
                e.stopPropagation();
            }
        });
    });

    // 3. Confirm Delete Dialogs
    const deleteForms = document.querySelectorAll(".form-delete-confirm");
    deleteForms.forEach(function (form) {
        form.addEventListener("submit", function (e) {
            e.preventDefault();
            const itemName = form.getAttribute("data-item-name") || "this record";
            
            if (window.Swal) {
                Swal.fire({
                    title: "Confirm Deletion",
                    text: `Are you sure you want to delete ${itemName}? This action cannot be undone.`,
                    icon: "warning",
                    showCancelButton: true,
                    confirmButtonColor: "#ef4444",
                    cancelButtonColor: "#64748b",
                    confirmButtonText: "Yes, Delete",
                    cancelButtonText: "Cancel"
                }).then((result) => {
                    if (result.isConfirmed) {
                        form.submit();
                    }
                });
            } else {
                if (confirm(`Are you sure you want to delete ${itemName}?`)) {
                    form.submit();
                }
            }
        });
    });
});
