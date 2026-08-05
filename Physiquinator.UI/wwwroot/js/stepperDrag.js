(() => {
    window.physiquinatorStepper = {
        init: function (element, dotNetRef) {
            if (!element) return;
            
            let startX = 0;
            let startY = 0;
            let startVal = 0;
            let step = 1;
            let min = 0;
            let isInteger = false;
            let hasDragged = false;
            let lastSteps = 0;
            let wasDragged = false;

            const onPointerMove = (e) => {
                const dx = e.clientX - startX;
                const dy = e.clientY - startY;
                
                // If they move more than 8px horizontally, start dragging
                if (!hasDragged && Math.abs(dx) > 8) {
                    hasDragged = true;
                    wasDragged = true;
                    element.classList.add('set-metric-stepper__value--dragging');
                    try {
                        element.setPointerCapture(e.pointerId);
                    } catch (_) {}
                }
                
                if (hasDragged) {
                    // Sensitivity: 12px of horizontal movement equals 1 step increment/decrement
                    const sensitivity = 12;
                    const steps = Math.round(dx / sensitivity);
                    
                    if (steps !== lastSteps) {
                        lastSteps = steps;
                        let newVal = startVal + (steps * step);
                        if (newVal < min) newVal = min;
                        
                        if (isInteger) {
                            newVal = Math.round(newVal);
                        } else {
                            newVal = Math.round(newVal * 10) / 10;
                        }
                        
                        if (steps > 0) {
                            element.classList.add('set-metric-stepper__value--dragging-right');
                            element.classList.remove('set-metric-stepper__value--dragging-left');
                        } else if (steps < 0) {
                            element.classList.add('set-metric-stepper__value--dragging-left');
                            element.classList.remove('set-metric-stepper__value--dragging-right');
                        } else {
                            element.classList.remove('set-metric-stepper__value--dragging-left', 'set-metric-stepper__value--dragging-right');
                        }

                        // Call Blazor to update value in real-time
                        dotNetRef.invokeMethodAsync('UpdateValueFromDrag', newVal);
                        
                        try {
                            if (navigator.vibrate) {
                                navigator.vibrate(5);
                            }
                        } catch (_) {}
                    }
                }
            };

            const onPointerUp = (e) => {
                element.removeEventListener('pointermove', onPointerMove);
                element.removeEventListener('pointerup', onPointerUp);
                element.removeEventListener('pointercancel', onPointerUp);
                
                try {
                    element.releasePointerCapture(e.pointerId);
                } catch (_) {}

                element.classList.remove(
                    'set-metric-stepper__value--dragging', 
                    'set-metric-stepper__value--dragging-left', 
                    'set-metric-stepper__value--dragging-right'
                );

                // Set a short timeout to clear wasDragged, so it covers the click event
                setTimeout(() => {
                    wasDragged = false;
                }, 50);
            };

            element.addEventListener('pointerdown', (e) => {
                if (e.button !== 0) return;
                
                startVal = parseFloat(element.getAttribute('data-value') || '0');
                step = parseFloat(element.getAttribute('data-step') || '1');
                min = parseFloat(element.getAttribute('data-min') || '0');
                isInteger = element.getAttribute('data-is-integer') === 'true';
                
                startX = e.clientX;
                startY = e.clientY;
                hasDragged = false;
                lastSteps = 0;
                wasDragged = false;

                element.addEventListener('pointermove', onPointerMove);
                element.addEventListener('pointerup', onPointerUp);
                element.addEventListener('pointercancel', onPointerUp);
            });

            element.addEventListener('click', (e) => {
                if (wasDragged) {
                    e.preventDefault();
                    e.stopPropagation();
                    wasDragged = false;
                }
            }, { capture: true });
        }
    };
})();
