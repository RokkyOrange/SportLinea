document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('rouletteForm');
    const mainWheel = document.getElementById('crazyWheel');
    const crazyBonusWheel = document.getElementById('crazyBonusWheel');
    const hotSpinsWheel = document.getElementById('hotSpinsWheel');
    const mainWheelWrap = document.getElementById('mainWheelWrap');
    const crazyBonusWheelWrap = document.getElementById('crazyBonusWheelWrap');
    const hotSpinsWheelWrap = document.getElementById('hotSpinsWheelWrap');
    const spinBtn = document.getElementById('spinBtn');
    const bonusSpinBtn = document.getElementById('bonusSpinBtn');
    const buyCrazyBtn = document.getElementById('buyCrazyBtn');
    const buyHotSpinsBtn = document.getElementById('buyHotSpinsBtn');
    const buyBonusGroup = document.getElementById('buyBonusGroup');
    const buyCrazyPrice = document.getElementById('buyCrazyPrice');
    const buyHotSpinsPrice = document.getElementById('buyHotSpinsPrice');
    const resultBox = document.getElementById('rouletteResult');
    const balanceEl = document.getElementById('rouletteBalance');
    const amountInput = document.getElementById('rouletteAmount');
    const stakeOptions = document.getElementById('rouletteStakeOptions');
    const stakeChips = stakeOptions ? [...stakeOptions.querySelectorAll('.roulette-stake-chip')] : [];
    const useFreeSpinCheck = document.getElementById('useFreeSpin');
    const betAmountGroup = document.getElementById('betAmountGroup');
    const panelTitle = document.getElementById('panelTitle');
    const panelDesc = document.getElementById('panelDesc');

    if (!form || !mainWheel) return;

    const mainSegmentAngle = 360 / 8;
    const crazyBonusSegmentAngle = 360 / 2;
    const hotSpinsSegmentAngle = 360 / 4;
    let spinning = false;
    let mainRotation = 0;
    let crazyBonusRotation = 0;
    let hotSpinsRotation = 0;
    let bonusPending = false;
    let activeBonusKind = null;
    let hotSpinsRemaining = 0;
    let bonusSessionWinnings = 0;

    const freeSpinAmount = parseInt(form.dataset.freeSpinAmount || '200', 10);
    const bonusBuyMultiplier = parseInt(form.dataset.buyMultiplier || '10', 10);
    const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const bonusUrl = form.dataset.bonusUrl;
    const buyBonusUrl = form.dataset.buyBonusUrl;
    const stateUrl = form.dataset.stateUrl;

    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        if (spinning || bonusPending) return;
        await doMainSpin();
    });

    bonusSpinBtn?.addEventListener('click', async () => {
        if (spinning) return;
        await doBonusSpin();
    });

    buyCrazyBtn?.addEventListener('click', async () => {
        if (spinning || bonusPending) return;
        await doBuyBonus('crazy');
    });

    buyHotSpinsBtn?.addEventListener('click', async () => {
        if (spinning || bonusPending) return;
        await doBuyBonus('hotspins');
    });

    useFreeSpinCheck?.addEventListener('change', () => {
        updateStakeControlsForFreeSpin();
    });

    stakeChips.forEach((chip) => {
        chip.addEventListener('click', () => {
            if (chip.disabled || useFreeSpinCheck?.checked) return;
            selectStake(parseInt(chip.dataset.amount, 10));
            updateBuyPrices();
        });
    });

    updateStakeAvailability();
    updateStakeControlsForFreeSpin();
    updateBuyPrices();
    restorePendingBonus();

    window.addEventListener('sportlinea:balance', (event) => {
        if (event.detail?.source === 'crazytime') return;
        updateStakeAvailability();
        updateBuyPrices();
    });

    function isPendingBonusBlockMessage(message) {
        return typeof message === 'string' && message.toLowerCase().includes('завершите бонусную');
    }

    async function restorePendingBonus() {
        if (!stateUrl) return false;

        try {
            const response = await fetch(stateUrl);
            const data = await response.json();
            if (!data.success || !data.hasPendingBonus) return false;

            const message = data.bonusKind === 'hotspins'
                ? `Продолжите HOT SPINS! Осталось ${data.bonusSpinsRemaining} вращений по ${Number(data.stakeAmount).toLocaleString('ru-RU')} ₽.`
                : `Продолжите CRAZY TIME! Крутите бонусное колесо (номинал ${Number(data.stakeAmount).toLocaleString('ru-RU')} ₽).`;

            if (data.bonusKind === 'hotspins') {
                bonusSessionWinnings = data.totalBonusWinnings || 0;
                startHotSpinsMode(message, data.bonusSpinsRemaining);
            } else {
                startCrazyBonusMode(message);
            }

            return true;
        } catch {
            return false;
        }
    }

    async function handleBlockedByPendingBonus(message) {
        const restored = await restorePendingBonus();
        if (restored) {
            showResult('У вас есть незавершённая бонусная игра. Продолжите её ниже.', 'jackpot');
            return true;
        }

        showResult(message, 'danger');
        return false;
    }

    function getSelectedStake() {
        return parseInt(amountInput?.value || '0', 10);
    }

    async function doBuyBonus(bonusKind) {
        if (!buyBonusUrl) return;

        const amount = getSelectedStake();
        if (!amount) {
            showResult('Выберите ставку', 'danger');
            return;
        }

        if (useFreeSpinCheck?.checked) {
            showResult('Покупка бонуса недоступна при бесплатном спине', 'danger');
            return;
        }

        const price = amount * bonusBuyMultiplier;
        if (price > getBalance()) {
            showResult(`Недостаточно средств. Нужно ${price.toLocaleString('ru-RU')} ₽`, 'danger');
            return;
        }

        const label = bonusKind === 'hotspins' ? 'HOT SPINS' : 'CRAZY';
        if (!confirm(`Купить ${label} за ${price.toLocaleString('ru-RU')} ₽?\nНоминал бонуса: ${amount.toLocaleString('ru-RU')} ₽`)) {
            return;
        }

        setBuyControlsEnabled(false);
        spinBtn.disabled = true;

        const body = new FormData();
        body.append('__RequestVerificationToken', token);
        body.append('amount', amount);
        body.append('bonusKind', bonusKind);

        try {
            const response = await fetch(buyBonusUrl, { method: 'POST', body });
            const data = await response.json();

            if (!data.success) {
                if (isPendingBonusBlockMessage(data.message)) {
                    await handleBlockedByPendingBonus(data.message);
                    resetMainControls();
                    return;
                }
                showResult(data.message, 'danger');
                resetMainControls();
                return;
            }

            if (balanceEl) updateBalance(data.balance);
            resultBox.style.display = 'none';
            document.getElementById('rouletteBonusResult')?.remove();

            if (data.bonusKind === 'hotspins') {
                startHotSpinsMode(data.message, data.bonusSpinsRemaining);
            } else {
                startCrazyBonusMode(data.message);
            }
        } catch {
            showResult('Ошибка соединения с сервером', 'danger');
            resetMainControls();
        }
    }

    function getBalance() {
        if (window.SportLineaBalance) return window.SportLineaBalance.read();
        return parseFloat(String(balanceEl?.textContent || '').replace(/\s/g, '').replace(',', '.')) || 0;
    }

    function selectStake(amount) {
        if (!amountInput) return;
        amountInput.value = amount;
        stakeChips.forEach((chip) => {
            chip.classList.toggle('active', parseInt(chip.dataset.amount, 10) === amount);
        });
        updateBuyPrices();
    }

    function updateStakeAvailability() {
        const balance = getBalance();
        let selected = parseInt(amountInput?.value || '0', 10);
        let highestAffordable = null;

        stakeChips.forEach((chip) => {
            const amount = parseInt(chip.dataset.amount, 10);
            const affordable = amount <= balance;
            chip.disabled = !affordable;
            chip.classList.toggle('disabled', !affordable);
            if (affordable) highestAffordable = amount;
        });

        if (!useFreeSpinCheck?.checked) {
            if (!selected || selected > balance) {
                selected = highestAffordable || parseInt(stakeChips[0]?.dataset.amount || '10', 10);
                selectStake(selected);
            } else {
                selectStake(selected);
            }
            spinBtn.disabled = !highestAffordable;
        }
    }

    function updateStakeControlsForFreeSpin() {
        const usingFreeSpin = !!useFreeSpinCheck?.checked;
        if (stakeOptions) stakeOptions.style.display = usingFreeSpin ? 'none' : 'flex';
        if (buyBonusGroup) buyBonusGroup.style.display = usingFreeSpin ? 'none' : 'block';
        if (usingFreeSpin && amountInput) {
            amountInput.value = freeSpinAmount;
        }
        if (usingFreeSpin) {
            spinBtn.disabled = false;
        } else {
            updateStakeAvailability();
        }
        updateBuyPrices();
    }

    function setStakeControlsEnabled(enabled) {
        stakeChips.forEach((chip) => {
            if (!chip.classList.contains('disabled')) {
                chip.disabled = !enabled;
            }
        });
    }

    function setMainControlsEnabled(enabled) {
        spinBtn.disabled = !enabled;
        setBuyControlsEnabled(enabled);
        if (!useFreeSpinCheck?.checked) {
            setStakeControlsEnabled(enabled);
        }
        if (useFreeSpinCheck) useFreeSpinCheck.disabled = !enabled;
    }

    function setBuyControlsEnabled(enabled) {
        if (!buyBonusGroup || bonusPending) return;
        const canBuy = enabled && !useFreeSpinCheck?.checked && canAffordBuyBonus();
        if (buyCrazyBtn) buyCrazyBtn.disabled = !canBuy;
        if (buyHotSpinsBtn) buyHotSpinsBtn.disabled = !canBuy;
    }

    function canAffordBuyBonus() {
        const amount = getSelectedStake();
        if (!amount) return false;
        return amount * bonusBuyMultiplier <= getBalance();
    }

    function updateBuyPrices() {
        const amount = getSelectedStake();
        const price = amount * bonusBuyMultiplier;
        const priceText = amount > 0
            ? `${price.toLocaleString('ru-RU')} ₽`
            : '—';

        if (buyCrazyPrice) buyCrazyPrice.textContent = priceText;
        if (buyHotSpinsPrice) buyHotSpinsPrice.textContent = priceText;
        setBuyControlsEnabled(!spinning);
    }

    async function doMainSpin() {
        if (!useFreeSpinCheck?.checked) {
            const balance = getBalance();
            const amount = parseInt(amountInput?.value || '0', 10);
            if (!amount || amount > balance) {
                showResult('Недостаточно средств для выбранной ставки', 'danger');
                return;
            }
        }

        const formData = new FormData(form);
        if (useFreeSpinCheck?.checked) {
            formData.set('useFreeSpin', 'true');
        }

        spinning = true;
        setMainControlsEnabled(false);
        spinBtn.textContent = 'Крутится...';
        resultBox.style.display = 'none';
        document.getElementById('rouletteBonusResult')?.remove();

        try {
            const response = await fetch(form.action, { method: 'POST', body: formData });
            const data = await response.json();

            if (!data.success) {
                if (isPendingBonusBlockMessage(data.message)) {
                    spinning = false;
                    await handleBlockedByPendingBonus(data.message);
                    resetMainControls();
                    return;
                }
                finishSpin(false, data.message, 'danger');
                return;
            }

            spinWheel(mainWheel, data.segmentIndex, mainSegmentAngle, mainRotation, (newRot) => {
                mainRotation = newRot;
            });

            setTimeout(() => {
                try {
                    if (balanceEl) updateBalance(data.balance);
                    if (data.freeSpinsRemaining !== undefined) updateFreeSpinsLabel(data.freeSpinsRemaining);

                    if (data.isBonus) {
                        if (data.bonusKind === 'hotspins') {
                            startHotSpinsMode(data.message, data.bonusSpinsRemaining, data.bonusMessage);
                        } else {
                            startCrazyBonusMode(data.message, data.bonusMessage);
                        }
                    } else {
                        const cssClass = data.multiplier === 0 ? 'danger' : data.multiplier >= 3 ? 'jackpot' : 'success';
                        finishSpin(true, data.message, cssClass, data.bonusMessage);
                    }
                } catch {
                    finishSpin(false, 'Ошибка отображения результата', 'danger');
                }
            }, 4500);
        } catch {
            finishSpin(false, 'Ошибка соединения с сервером', 'danger');
        }
    }

    async function doBonusSpin() {
        spinning = true;
        bonusSpinBtn.disabled = true;
        bonusSpinBtn.textContent = 'Крутится...';
        resultBox.style.display = 'none';
        document.getElementById('rouletteBonusResult')?.remove();

        const wheel = activeBonusKind === 'hotspins' ? hotSpinsWheel : crazyBonusWheel;
        const segmentAngle = activeBonusKind === 'hotspins' ? hotSpinsSegmentAngle : crazyBonusSegmentAngle;
        const currentRotation = activeBonusKind === 'hotspins' ? hotSpinsRotation : crazyBonusRotation;
        const setRotation = activeBonusKind === 'hotspins'
            ? (value) => { hotSpinsRotation = value; }
            : (value) => { crazyBonusRotation = value; };

        try {
            const body = new FormData();
            body.append('__RequestVerificationToken', token);

            const response = await fetch(bonusUrl, { method: 'POST', body });
            const data = await response.json();

            if (!data.success) {
                if (data.message?.includes('Нет активной бонусной')) {
                    endBonusMode('Бонусная игра уже завершена.', 'success');
                    return;
                }
                finishBonusSpin(data.message, 'danger');
                return;
            }

            spinWheel(wheel, data.segmentIndex, segmentAngle, currentRotation, setRotation);

            setTimeout(() => {
                if (balanceEl) updateBalance(data.balance);

                if (data.bonusKind === 'hotspins') {
                    hotSpinsRemaining = data.spinsRemaining;
                    if (data.totalBonusWinnings !== undefined) {
                        bonusSessionWinnings = data.totalBonusWinnings;
                    }
                    const cssClass = data.isSensation ? 'jackpot' : data.multiplier >= 5 ? 'success' : 'danger';

                    if (data.bonusComplete) {
                        endBonusMode(data.message, cssClass, data.bonusTotalSummary);
                    } else {
                        spinning = false;
                        bonusSpinBtn.disabled = false;
                        updateBonusSpinButton();
                        showResult(data.message, cssClass);
                    }
                } else {
                    const cssClass = data.isBankrupt ? 'danger' : data.isJackpot ? 'jackpot' : 'success';
                    endBonusMode(data.message, cssClass, data.bonusTotalSummary);
                }
            }, 4500);
        } catch {
            finishBonusSpin('Ошибка соединения с сервером', 'danger');
        }
    }

    function spinWheel(wheel, segmentIndex, segmentAngle, currentRot, setRot) {
        const segmentCenter = segmentIndex * segmentAngle;
        const jitter = (Math.random() - 0.5) * segmentAngle * 0.62;
        const targetPoint = segmentCenter + jitter;
        const targetAngle = (360 - targetPoint + 360) % 360;
        const currentMod = ((currentRot % 360) + 360) % 360;
        let delta = targetAngle - currentMod;
        if (delta <= 0) delta += 360;
        const newRot = currentRot + 360 * 6 + delta;
        setRot(newRot);
        wheel.style.transform = `rotate(${newRot}deg)`;
    }

    function hideAllBonusWheels() {
        crazyBonusWheelWrap.style.display = 'none';
        hotSpinsWheelWrap.style.display = 'none';
    }

    function startCrazyBonusMode(message, bonusMessage) {
        bonusPending = true;
        activeBonusKind = 'crazy';
        bonusSessionWinnings = 0;
        spinning = false;
        spinBtn.style.display = 'none';
        betAmountGroup.style.display = 'none';
        if (buyBonusGroup) buyBonusGroup.style.display = 'none';
        bonusSpinBtn.style.display = 'block';
        bonusSpinBtn.disabled = false;
        updateBonusSpinButton();

        mainWheelWrap.style.display = 'none';
        hideAllBonusWheels();
        crazyBonusWheelWrap.style.display = 'block';

        panelTitle.textContent = '🔥 CRAZY TIME — Бонус!';
        panelDesc.textContent = '×1000 от ставки (очень редко) или BUST — проигрыш ставки.';
        showResult(message, 'jackpot');
        showBonusToast(bonusMessage);
    }

    function startHotSpinsMode(message, spinsRemaining, bonusMessage) {
        bonusPending = true;
        activeBonusKind = 'hotspins';
        hotSpinsRemaining = spinsRemaining;
        bonusSessionWinnings = 0;
        spinning = false;
        spinBtn.style.display = 'none';
        betAmountGroup.style.display = 'none';
        if (buyBonusGroup) buyBonusGroup.style.display = 'none';
        bonusSpinBtn.style.display = 'block';
        bonusSpinBtn.disabled = false;
        updateBonusSpinButton();

        mainWheelWrap.style.display = 'none';
        hideAllBonusWheels();
        hotSpinsWheelWrap.style.display = 'block';

        panelTitle.textContent = '🔥 HOT SPINS!';
        panelDesc.textContent = '5 бесплатных вращений: много ×0, ×5 и очень редкий SENSATION (×25 от ставки).';
        showResult(message, 'jackpot');
        showBonusToast(bonusMessage);
    }

    function updateBonusSpinButton() {
        if (activeBonusKind === 'hotspins' && hotSpinsRemaining > 0) {
            bonusSpinBtn.textContent = `🔥 HOT SPINS (${hotSpinsRemaining} осталось)`;
        } else if (activeBonusKind === 'crazy') {
            bonusSpinBtn.textContent = '🔥 Крутить CRAZY!';
        } else {
            bonusSpinBtn.textContent = '🔥 Крутить бонус!';
        }
    }

    function endBonusMode(message, type, bonusTotalSummary) {
        bonusPending = false;
        activeBonusKind = null;
        hotSpinsRemaining = 0;
        bonusSessionWinnings = 0;
        spinning = false;

        hideAllBonusWheels();
        mainWheelWrap.style.display = 'block';

        spinBtn.style.display = 'block';
        bonusSpinBtn.style.display = 'none';
        betAmountGroup.style.display = 'block';
        if (buyBonusGroup) buyBonusGroup.style.display = 'block';
        resetMainControls();

        panelTitle.textContent = 'Испытайте удачу';
        panelDesc.textContent = 'Сектора: много ×0, ×2, максимум ×3. HOT SPINS и CRAZY — бонусные игры!';

        showResult(message, type, bonusTotalSummary);
    }

    function resetMainControls() {
        spinning = false;
        spinBtn.disabled = false;
        spinBtn.textContent = '🎰 Крутить!';
        updateStakeControlsForFreeSpin();
    }

    function finishSpin(success, text, type, bonusMessage) {
        resetMainControls();
        if (text) showResult(text, type);
        showBonusToast(bonusMessage);
    }

    function finishBonusSpin(text, type) {
        spinning = false;
        bonusSpinBtn.disabled = false;
        updateBonusSpinButton();
        showResult(text, type);
    }

    function showBonusToast(message) {
        if (!message) return;

        document.getElementById('rouletteBonusToast')?.remove();

        const container = document.querySelector('.site-main .container');
        if (!container) return;

        const alert = document.createElement('div');
        alert.id = 'rouletteBonusToast';
        alert.className = 'alert alert-bonus alert-dismissible fade show';
        alert.setAttribute('role', 'alert');
        alert.innerHTML = `${message}<button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Закрыть"></button>`;

        const flashAlerts = container.querySelector('.alert');
        if (flashAlerts) {
            flashAlerts.insertAdjacentElement('beforebegin', alert);
        } else {
            container.insertBefore(alert, container.firstChild);
        }

        alert.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }

    function showResult(text, type, summaryText) {
        resultBox.style.display = 'block';
        resultBox.className = 'roulette-result mt-3 result-' + type;
        resultBox.textContent = text;

        document.getElementById('rouletteBonusResult')?.remove();

        if (summaryText) {
            const bonusBox = document.createElement('div');
            bonusBox.id = 'rouletteBonusResult';
            bonusBox.className = 'roulette-result mt-2 result-bonus';
            bonusBox.textContent = summaryText;
            resultBox.insertAdjacentElement('afterend', bonusBox);
        }
    }

    function updateBalance(balance) {
        if (window.SportLineaBalance) {
            window.SportLineaBalance.sync(balance, 'crazytime');
        } else if (balanceEl) {
            balanceEl.textContent = balance.toLocaleString('ru-RU', { minimumFractionDigits: 2 }) + ' ₽';
        }
        updateStakeAvailability();
        updateBuyPrices();
    }

    function updateFreeSpinsLabel(remaining) {
        if (!useFreeSpinCheck) return;
        const label = useFreeSpinCheck.closest('.form-check')?.querySelector('label');
        if (remaining > 0) {
            if (label) label.textContent = `Бесплатный спин ${freeSpinAmount} ₽ (осталось: ${remaining})`;
            useFreeSpinCheck.disabled = false;
        } else {
            useFreeSpinCheck.checked = false;
            useFreeSpinCheck.disabled = true;
            if (label) label.textContent = 'Бесплатные спины закончились';
            updateStakeControlsForFreeSpin();
        }
    }
});
