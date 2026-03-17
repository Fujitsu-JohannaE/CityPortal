/**
 * conditional-fields.js
 *
 * Reads data-conditional-on / data-conditional-value attributes from
 * .form-field-wrapper elements and shows/hides them reactively.
 *
 * Usage in Razor (_FormField.cshtml):
 *   <div class="form-field-wrapper"
 *        data-conditional-on="fieldKey"
 *        data-conditional-value="expected value"
 *        style="display:none">
 */
document.addEventListener('DOMContentLoaded', () => {
    const form = document.querySelector('form');
    if (!form) return;

    const conditionalWrappers = form.querySelectorAll('[data-conditional-on]');
    if (!conditionalWrappers.length) return;

    /**
     * Get the current value of a field by its FieldKey.
     * Handles text/select/radio/checkbox.
     */
    function getFieldValue(fieldKey) {
        // Try radio buttons first
        const radio = form.querySelector(`input[name="Values[${fieldKey}]"]:checked`);
        if (radio) return radio.value;

        // Try checkbox
        const checkbox = form.querySelector(
            `input[type="checkbox"][name="Values[${fieldKey}]"]`
        );
        if (checkbox) return checkbox.checked ? 'true' : '';

        // Try any other input / select / textarea
        const el = form.querySelector(
            `[name="Values[${fieldKey}]"]`
        );
        return el ? el.value : '';
    }

    /**
     * Evaluate all conditional wrappers and show/hide accordingly.
     * Also disables inputs inside hidden wrappers so they are not submitted.
     */
    function evaluate() {
        conditionalWrappers.forEach(wrapper => {
            const watchKey = wrapper.dataset.conditionalOn;
            const watchValue = wrapper.dataset.conditionalValue; // may be undefined

            const currentValue = getFieldValue(watchKey);

            const shouldShow = watchValue
                ? currentValue === watchValue          // show when specific value matched
                : currentValue !== '';                 // show when any value present

            wrapper.style.display = shouldShow ? '' : 'none';

            // Disable fields inside hidden wrappers so they aren't submitted
            // and don't trigger browser required-field validation
            wrapper.querySelectorAll('input, select, textarea').forEach(input => {
                input.disabled = !shouldShow;
            });
        });
    }

    // Evaluate on page load (handles pre-filled values on validation error return)
    evaluate();

    // Re-evaluate on any user interaction
    form.addEventListener('change', evaluate);
    form.addEventListener('input', evaluate);
});