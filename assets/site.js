// FluxTV — site : intro animée (la même que dans l'app), écrans, petites interactions.
(() => {
  if (window.fluxAilleurs) return; // la page part vers l'autre langue : rien à animer ni à compter ici
  window.fluxIntro = true;
  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => [...r.querySelectorAll(s)];
  const calm = matchMedia('(prefers-reduced-motion: reduce)').matches;
  const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
  const LANG = document.documentElement.lang === 'en' ? 'en' : 'fr';
  const T = (fr, en) => (LANG === 'en' ? en : fr);

  // ─────────────── Fond étoilé : nébuleuse, étoiles en vitesse lumière, ondes, étincelles ───────────────
  const Field = (() => {
    const cv = $('#fx'); const g = cv.getContext('2d');
    let w = 0, h = 0, dpr = 1, cx = 0, cy = 0, warp = 1, flash = 0, t0 = 0, last = 0, nextRing = 0, raf = 0, running = false, visible = true;
    const stars = [], rings = [], sparks = [];
    const cols = ['#ffffff', '#c7d2fe', '#e9d5ff', '#8e97ff', '#f0abfc'];
    const spawn = (s, any) => {
      s.a = Math.random() * Math.PI * 2; s.d = any ? Math.random() : Math.random() * 0.06;
      s.v = 0.1 + Math.random() * 0.38; s.z = 0.7 + Math.random() * 1.8; s.c = cols[(Math.random() * cols.length) | 0];
    };
    function resize() {
      const r = cv.getBoundingClientRect();
      dpr = Math.min(2, window.devicePixelRatio || 1);
      w = cv.width = Math.max(1, Math.round(r.width * dpr)); h = cv.height = Math.max(1, Math.round(r.height * dpr));
      center();
    }
    function center() {
      const m = $('#mark'); const r = cv.getBoundingClientRect();
      if (!m) { cx = w / 2; cy = h * 0.4; return; }
      const b = m.getBoundingClientRect();
      cx = (b.left + b.width / 2 - r.left) * dpr; cy = (b.top + b.height / 2 - r.top) * dpr;
    }
    function frame(now) {
      if (!running) return;
      if (!t0) { t0 = now; last = now; nextRing = now + 2600; }
      const dt = Math.min(0.05, (now - last) / 1000); last = now;
      const t = (now - t0) / 1000;
      g.globalCompositeOperation = 'source-over'; g.globalAlpha = 1;
      g.clearRect(0, 0, w, h);
      const R = Math.min(w, h) * 0.6 * (0.9 + 0.1 * Math.sin(t * 1.6));
      let gr = g.createRadialGradient(cx, cy, 0, cx, cy, R);
      gr.addColorStop(0, 'rgba(109,76,245,.55)'); gr.addColorStop(0.45, 'rgba(91,92,240,.18)'); gr.addColorStop(1, 'rgba(0,0,0,0)');
      g.fillStyle = gr; g.fillRect(0, 0, w, h);
      gr = g.createRadialGradient(cx + Math.cos(t * 0.6) * R * 0.3, cy + Math.sin(t * 0.5) * R * 0.2, 0, cx, cy, R * 0.8);
      gr.addColorStop(0, 'rgba(244,63,94,.12)'); gr.addColorStop(1, 'rgba(0,0,0,0)'); g.fillStyle = gr; g.fillRect(0, 0, w, h);
      warp += (1 - warp) * Math.min(1, dt * 2.5);
      const reach = Math.max(w, h) * 0.78;
      g.globalCompositeOperation = 'lighter'; g.lineCap = 'round';
      for (const s of stars) {
        const prev = s.d; s.d += s.v * (0.2 + s.d * 2.2) * warp * dt;
        if (s.d > 1.15) { spawn(s, false); continue; }
        const ca = Math.cos(s.a), sa = Math.sin(s.a), tail = 1 + (warp - 1) * 0.9;
        const x2 = cx + ca * s.d * reach, y2 = cy + sa * s.d * reach;
        const x1 = x2 - (x2 - (cx + ca * prev * reach)) * tail, y1 = y2 - (y2 - (cy + sa * prev * reach)) * tail;
        g.strokeStyle = s.c; g.globalAlpha = Math.min(1, s.d * 4) * (0.4 + 0.6 * Math.min(1, s.d));
        g.lineWidth = s.z * dpr * (0.6 + s.d * 1.4);
        g.beginPath(); g.moveTo(x1, y1); g.lineTo(x2, y2); g.stroke();
      }
      if (now > nextRing) { rings.push({ b: now, k: 0.45 }); nextRing = now + 2300; }
      for (let i = rings.length - 1; i >= 0; i--) {
        const r = rings[i]; const p = (now - r.b) / 1700;
        if (p >= 1) { rings.splice(i, 1); continue; }
        const e = 1 - Math.pow(1 - p, 3);
        g.globalAlpha = (1 - p) * r.k; g.strokeStyle = '#b4a5ff'; g.lineWidth = (8 * (1 - p) + 1) * dpr;
        g.beginPath(); g.arc(cx, cy, (70 + 620 * e) * dpr, 0, Math.PI * 2); g.stroke();
      }
      for (let i = sparks.length - 1; i >= 0; i--) {
        const s = sparks[i]; s.life -= dt; if (s.life <= 0) { sparks.splice(i, 1); continue; }
        s.vx *= 0.96; s.vy = s.vy * 0.96 + 40 * dt * dpr; s.x += s.vx * dt; s.y += s.vy * dt;
        g.globalAlpha = Math.min(1, s.life * 1.6); g.fillStyle = s.c;
        g.beginPath(); g.arc(s.x, s.y, s.r * dpr, 0, 7); g.fill();
      }
      if (flash > 0.01) { g.globalCompositeOperation = 'source-over'; g.globalAlpha = flash; g.fillStyle = '#b9a8ff'; g.fillRect(0, 0, w, h); flash *= 1 - Math.min(1, dt * 4); }
      raf = requestAnimationFrame(frame);
    }
    function play() { if (!running && visible && !document.hidden) { running = true; last = performance.now(); raf = requestAnimationFrame(frame); } }
    function pause() { running = false; cancelAnimationFrame(raf); }
    return {
      start() {
        resize(); addEventListener('resize', resize);
        const n = innerWidth < 700 ? 150 : 260;
        for (let i = 0; i < n; i++) { const s = {}; spawn(s, true); stars.push(s); }
        // Pas de calcul quand l'intro n'est plus à l'écran ou que l'onglet est caché
        new IntersectionObserver(([e]) => { visible = e.isIntersecting; visible ? play() : pause(); }).observe(cv);
        document.addEventListener('visibilitychange', () => (document.hidden ? pause() : play()));
        play();
      },
      center,
      burst(k = 1) {
        center();
        rings.push({ b: performance.now(), k }); warp = 7 * k + 1; flash = 0.18 * k;
        for (let i = 0; i < 140 * k; i++) {
          const a = Math.random() * Math.PI * 2, v = (200 + Math.random() * 700) * dpr;
          sparks.push({ x: cx, y: cy, vx: Math.cos(a) * v, vy: Math.sin(a) * v, r: 0.8 + Math.random() * 2.2, life: 0.6 + Math.random() * 0.9, c: cols[(Math.random() * cols.length) | 0] });
        }
      },
    };
  })();

  // ─────────────── Intro : même déroulé que l'app ───────────────
  async function intro() {
    const mark = $('#mark'), word = $('#word'), tag = $('#tag'), rays = $('.hero-rays');
    if (calm) {
      for (const el of [mark, rays]) el.style.opacity = 1;
      $('#spPlay').style.transform = 'none';
      for (const id of ['#spW1', '#spW2']) $(id).style.strokeDashoffset = 0;
      for (const s of $$('span', word)) { s.style.opacity = 1; s.style.transform = 'none'; }
      tag.classList.add('in'); $$('.reveal').forEach((r) => r.classList.add('in'));
      return;
    }
    Field.start();
    rays.animate([{ opacity: 0 }, { opacity: 1 }], { duration: 1600, fill: 'forwards', easing: 'ease-out' });
    // 1. Emblème : bascule 3D avec rebond
    mark.animate([
      { opacity: 0, transform: 'perspective(800px) rotateY(-120deg) rotateX(25deg) scale(.25)' },
      { opacity: 1, transform: 'perspective(800px) rotateY(12deg) rotateX(-4deg) scale(1.1)', offset: 0.7 },
      { opacity: 1, transform: 'perspective(800px) rotateY(0) rotateX(0) scale(1)' },
    ], { duration: 950, delay: 150, easing: 'cubic-bezier(.2,0,0,1)', fill: 'both' });
    await sleep(700);
    $('#spPlay').animate([{ transform: 'scale(0) rotate(-40deg)' }, { transform: 'scale(1.25) rotate(6deg)', offset: .65 }, { transform: 'scale(1) rotate(0)' }], { duration: 600, easing: 'ease-out', fill: 'forwards' });
    $('#spW1').animate([{ strokeDashoffset: 14 }, { strokeDashoffset: 0 }], { duration: 420, delay: 250, easing: 'ease-out', fill: 'forwards' });
    $('#spW2').animate([{ strokeDashoffset: 24 }, { strokeDashoffset: 0 }], { duration: 520, delay: 420, easing: 'ease-out', fill: 'forwards' });
    await sleep(320);
    // 2. Salve : onde de choc, étincelles, reflet, battement, lettres
    Field.burst(1);
    mark.classList.add('shine');
    mark.animate([{ transform: 'scale(1)' }, { transform: 'scale(1.12)' }, { transform: 'scale(1)' }], { duration: 520, easing: 'cubic-bezier(.34,1.56,.64,1)', composite: 'add' });
    word.classList.add('in');
    await sleep(500);
    tag.classList.add('in');
    await sleep(250);
    $$('.reveal').forEach((r) => r.classList.add('in'));
    // Les arcs de diffusion « émettent » régulièrement
    setInterval(() => $('#spW2').animate([{ strokeOpacity: .55 }, { strokeOpacity: 1 }, { strokeOpacity: .55 }], { duration: 900 }), 1800);
  }

  // ─────────────── Barre du haut ───────────────
  const top = $('#top');
  const onScroll = () => top.classList.toggle('solid', scrollY > 30);
  addEventListener('scroll', onScroll, { passive: true }); onScroll();

  // Menu : se ferme en choisissant un lien, avec Échap ou en touchant à côté ; la page ne défile pas dessous.
  // « Partager » le laisse ouvert, pour qu'on voie « Lien copié ».
  const menu = $('#menu');
  if (menu) {
    const sync = () => { document.documentElement.classList.toggle('menu-open', menu.open); top.classList.toggle('menu-on', menu.open); };
    menu.addEventListener('toggle', sync);
    $$('.menu-panel a:not([data-partage])', menu).forEach((a) => a.addEventListener('click', () => { menu.open = false; }));
    $('.menu-veil', menu).addEventListener('click', () => { menu.open = false; });
    addEventListener('keydown', (e) => { if (e.key === 'Escape' && menu.open) { menu.open = false; $('summary', menu).focus(); } });
  }

  // ─────────────── Écrans de l'app : onglets et défilement automatique ───────────────
  const stage = $('[data-tabs]');
  if (stage) {
    const tabs = $$('[data-tab]', stage), shots = $$('[data-shot]', stage);
    let cur = 0, timer = 0;
    tabs.forEach((t) => { const b = document.createElement('span'); b.className = 'bar'; b.innerHTML = '<i></i>'; t.appendChild(b); });
    const show = (i, auto) => {
      cur = (i + tabs.length) % tabs.length;
      tabs.forEach((t, k) => t.setAttribute('aria-selected', String(k === cur)));
      shots.forEach((s, k) => s.classList.toggle('is-on', k === cur));
      const bar = $('.bar i', tabs[cur]);
      $$('.bar i', stage).forEach((b) => b.getAnimations().forEach((a) => a.cancel()));
      if (!calm) bar.animate([{ transform: 'scaleX(0)' }, { transform: 'scaleX(1)' }], { duration: 5000, easing: 'linear' });
      clearTimeout(timer);
      if (!calm && auto !== false) timer = setTimeout(() => show(cur + 1), 5000);
    };
    tabs.forEach((t, k) => {
      t.addEventListener('click', () => show(k, false));
      t.addEventListener('keydown', (e) => {
        if (e.key === 'ArrowDown' || e.key === 'ArrowRight') { e.preventDefault(); show(cur + 1, false); tabs[cur].focus(); }
        if (e.key === 'ArrowUp' || e.key === 'ArrowLeft') { e.preventDefault(); show(cur - 1, false); tabs[cur].focus(); }
      });
    });
    // Le défilement automatique ne démarre qu'une fois la télé à l'écran
    new IntersectionObserver(([e], o) => { if (e.isIntersecting) { show(0); o.disconnect(); } }, { threshold: 0.35 }).observe(stage);
  }

  // ─────────────── Adresse à recopier dans Downloader ───────────────
  $$('code.copy').forEach((c) => c.addEventListener('click', async () => {
    try { await navigator.clipboard.writeText(c.dataset.copy); c.classList.add('done'); setTimeout(() => c.classList.remove('done'), 1800); } catch (e) { /* copie refusée : l'adresse reste lisible */ }
  }));

  // ─────────────── Apparition des blocs au défilement ───────────────
  if (!calm && 'IntersectionObserver' in window) {
    const els = $$('.head, .feat, .plat, .home-show, .home-points > div, .steps li, .home-dl, .faq details, .policy');
    els.forEach((el) => el.classList.add('rise'));
    const io = new IntersectionObserver((entries) => {
      for (const e of entries) if (e.isIntersecting) {
        const sibs = [...e.target.parentElement.children].filter((x) => x.classList.contains('rise'));
        e.target.style.transitionDelay = Math.min(5, Math.max(0, sibs.indexOf(e.target))) * 70 + 'ms';
        e.target.classList.add('in'); io.unobserve(e.target);
      }
    }, { rootMargin: '0px 0px -8% 0px' });
    els.forEach((el) => io.observe(el));
  }

  // ─────────────── Compteur de téléchargements, en direct ───────────────
  // Service Abacus : un compteur « total » (toutes plateformes) et un par plateforme. Le total arrive
  // en direct (flux d'événements) ; sans flux, il est relu toutes les 30 s. Rien ne s'affiche tant
  // qu'aucun téléchargement n'a eu lieu, ni si le service ne répond pas.
  // Sans pastille de compteur dans la page, aucune connexion n'est ouverte : les clics restent comptés.
  const Compteur = (() => {
    const API = 'https://abacus.jasoncameron.dev', NS = 'fluxtv-mkzrt22';
    const blocs = $$('[data-compteur]');
    const fmt = new Intl.NumberFormat(T('fr-FR', 'en-US'));
    let shown = -1, target = 0, es = null, poll = 0, anim = 0;

    function render(v) {
      for (const b of blocs) {
        // Espace fine insécable absente des polices du site : espace insécable classique
        $('[data-total]', b).textContent = fmt.format(v).replace(/\u202f/g, '\u00a0');
        $('[data-libelle]', b).textContent = v > 1 ? T('téléchargements', 'downloads') : T('téléchargement', 'download');
      }
    }
    function show(v) {
      if (typeof v !== 'number' || v < 0 || v === target) return;
      const from = shown < 0 ? v : shown;
      target = v;
      if (v >= 1) blocs.forEach((b) => { if (b.hidden) { b.hidden = false; requestAnimationFrame(() => b.classList.add('on')); } });
      // Le chiffre défile jusqu'à la nouvelle valeur, et la pastille « bat » à chaque téléchargement
      cancelAnimationFrame(anim);
      if (calm || from === v) { shown = v; render(v); return; }
      if (v > from) blocs.forEach((b) => { b.classList.remove('bump'); void b.offsetWidth; b.classList.add('bump'); });
      const t0 = performance.now(), d = 700;
      const step = (now) => {
        const k = Math.min(1, (now - t0) / d), e = 1 - Math.pow(1 - k, 3);
        shown = Math.round(from + (v - from) * e); render(shown);
        if (k < 1) anim = requestAnimationFrame(step);
      };
      anim = requestAnimationFrame(step);
    }
    const read = () => fetch(`${API}/get/${NS}/total`).then((r) => r.json()).then((d) => show(d.value)).catch(() => {});
    function startPoll() { if (!poll) { read(); poll = setInterval(read, 30000); } }
    function stop() { if (es) { es.close(); es = null; } clearInterval(poll); poll = 0; }
    function connect() {
      stop();
      if (!blocs.length) return;
      if (!('EventSource' in window)) return startPoll();
      es = new EventSource(`${API}/stream/${NS}/total`);
      es.onmessage = (e) => { try { show(JSON.parse(e.data).value); } catch (err) { /* message illisible : ignoré */ } };
      es.onerror = () => { if (es && es.readyState === EventSource.CLOSED) { es = null; startPoll(); } };
    }
    // Onglet caché : plus de connexion ouverte ; au retour, valeur à jour aussitôt
    document.addEventListener('visibilitychange', () => (document.hidden ? stop() : connect()));

    /** Téléchargement lancé : +1 pour la plateforme et pour le total, une fois par navigateur. */
    function hit(platform) {
      const mark = 'fluxtv-compte-' + platform;
      try { if (localStorage.getItem(mark)) return; localStorage.setItem(mark, '1'); } catch (e) { /* navigation privée : compté quand même */ }
      const go = (key) => fetch(`${API}/hit/${NS}/${key}`, { keepalive: true }).then((r) => r.json());
      go(platform).catch(() => {});
      // L'assistant FluxTV Home, le soutien et les dons sont comptés à part, pas dans le total
      if (platform !== 'assistant' && platform !== 'soutien' && !platform.startsWith('don-')) go('total').then((d) => show(d.value)).catch(() => {});
    }
    $$('[data-compte]').forEach((a) => a.addEventListener('click', () => hit(a.dataset.compte)));

    /** Visite : +1 une fois par onglet, et +1 pour le réseau d'où vient le lien (?s=reddit…). */
    function visite() {
      const q = new URLSearchParams(location.search), src = (q.get('s') || '').toLowerCase();
      // L'adresse partagée ensuite ne garde pas la source
      if (q.has('s')) { q.delete('s'); const reste = q.toString(); history.replaceState(null, '', location.pathname + (reste ? '?' + reste : '') + location.hash); }
      if (navigator.webdriver) return; // robots et tests automatiques
      try { if (sessionStorage.getItem('fluxtv-visite')) return; sessionStorage.setItem('fluxtv-visite', '1'); } catch (e) { /* compté quand même */ }
      const go = (key) => fetch(`${API}/hit/${NS}/${key}`).catch(() => {});
      go('visites');
      go('lang-' + LANG);
      if (/^[a-z0-9-]{2,20}$/.test(src)) go('src-' + src);
    }
    return { connect, visite };
  })();
  Compteur.connect();
  Compteur.visite();

  /* Partager : feuille de partage du téléphone, sinon le lien est copié */
  const PARTAGE = { title: 'FluxTV', text: T('FluxTV, un lecteur IPTV gratuit pour Xbox, PC, Android TV et iPhone', 'FluxTV, a free IPTV player for Xbox, PC, Android TV and iPhone'),
    url: T('https://mkzrt22.github.io/FluxTV-iOS/?s=partage', 'https://mkzrt22.github.io/FluxTV-iOS/en/?s=partage') };
  $$('[data-partage]').forEach((b) => b.addEventListener('click', async (ev) => {
    ev.preventDefault();
    if (navigator.share) { try { await navigator.share(PARTAGE); } catch (e) { /* partage annulé */ } return; }
    try { await navigator.clipboard.writeText(PARTAGE.url); } catch (e) { return; }
    const label = $('span', b) || b, avant = label.textContent;
    label.textContent = T('Lien copié ✓', 'Link copied ✓'); b.classList.add('copied');
    setTimeout(() => { label.textContent = avant; b.classList.remove('copied'); }, 2000);
  }));

  // ─────────────── Langue : demandée au premier passage, puis mémorisée ───────────────
  // La page est déjà dans la langue du navigateur (script en tête de page) ; ce bandeau la fait confirmer ou changer.
  const LANGUE = 'fluxtv-lang';
  const garder = (l) => { try { localStorage.setItem(LANGUE, l); } catch (e) { /* stockage refusé : la langue du navigateur s'applique */ } };
  $$('[data-lang]').forEach((a) => a.addEventListener('click', () => garder(a.dataset.lang)));
  function choixLangue() {
    try { if (localStorage.getItem(LANGUE)) return; } catch (e) { return; }
    const autre = document.documentElement.dataset.autre;
    const choix = (l, nom) => (l === LANG
      ? `<button type="button" class="on" lang="${l}" aria-current="true">${nom}</button>`
      : `<a href="${autre}?lang=${l}" hreflang="${l}" lang="${l}">${nom}</a>`);
    const box = document.createElement('div');
    box.className = 'lang-pick';
    box.setAttribute('role', 'dialog');
    box.setAttribute('aria-label', 'Langue · Language');
    box.innerHTML = '<p><svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3c2.5 2.6 3.8 5.6 3.8 9s-1.3 6.4-3.8 9c-2.5-2.6-3.8-5.6-3.8-9S9.5 5.6 12 3z"/></svg>'
      + '<span><span lang="fr">Choisis ta langue</span> · <span lang="en">Choose your language</span></span></p>'
      + `<div class="lang-btns">${choix('fr', 'Français')}${choix('en', 'English')}</div>`
      + `<button type="button" class="lang-x" aria-label="${T('Fermer', 'Close')}">×</button>`;
    const fermer = () => { garder(LANG); box.classList.remove('in'); setTimeout(() => box.remove(), 500); removeEventListener('keydown', echap); };
    const echap = (e) => { if (e.key === 'Escape' && !($('#menu') || {}).open) fermer(); };
    $('.lang-btns .on', box).addEventListener('click', fermer);
    $('.lang-x', box).addEventListener('click', fermer);
    $('.lang-btns a', box).addEventListener('click', () => garder(LANG === 'fr' ? 'en' : 'fr'));
    addEventListener('keydown', echap);
    document.body.append(box);
    requestAnimationFrame(() => requestAnimationFrame(() => box.classList.add('in')));
  }
  // Après l'intro, pour ne pas la masquer
  setTimeout(choixLangue, calm ? 300 : 2600);

  intro();
})();
