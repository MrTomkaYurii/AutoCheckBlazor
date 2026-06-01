// GitGraph Blazor Interop
// Renders an interactive git graph and reports commit clicks back to Blazor.

window.gitGraphInterop = {

    _instance: null,

    // data = { branches: [{ name, commits: [{ sha, message, date }] }] }
    init: function (containerId, data, dotNetRef) {
        const container = document.getElementById(containerId);
        if (!container) return;

        container.innerHTML = '';

        const { createGitgraph, templateExtend, TemplateName } = window.GitgraphJS || window['@gitgraph/js'] || {};
        if (!createGitgraph) {
            container.innerHTML = '<div style="color:var(--st-red);font-size:13px;padding:10px">GitGraph не завантажено</div>';
            return;
        }

        const template = templateExtend(TemplateName.Metro, {
            colors: ['#76c7ad', '#e7c14b', '#e8806b', '#9b8de8', '#5bc4e8'],
            branch: {
                lineWidth: 2,
                label: {
                    display: true,
                    bgColor: 'transparent',
                    strokeColor: 'transparent',
                    font: '600 11px JetBrains Mono, monospace',
                    color: '#aebbb5',
                },
                spacing: 40,
            },
            commit: {
                dot: {
                    size: 9,
                    strokeWidth: 2,
                    strokeColor: 'transparent',
                },
                message: {
                    display: true,
                    font: '500 12px Inter, sans-serif',
                    color: '#eaf3ef',
                    displayHash: false,
                    displayAuthor: false,
                },
                spacing: 40,
            },
            tag: { display: false },
        });

        const graph = createGitgraph(container, {
            template,
            orientation: 'vertical',
            author: '',
        });

        // Будуємо гілки
        const branchMap = {};
        const allCommits = [];

        // Спочатку створюємо головну гілку (перша або main/master)
        const mainBranchData = data.branches.find(b =>
            b.name === 'main' || b.name === 'master') || data.branches[0];

        if (!mainBranchData) return;

        // Збираємо всі коміти з датами для сортування
        data.branches.forEach(b => {
            b.commits.forEach(c => {
                if (!allCommits.find(x => x.sha === c.sha))
                    allCommits.push({ ...c, branch: b.name });
            });
        });
        allCommits.sort((a, b) => new Date(a.date) - new Date(b.date));

        // Створюємо гілку main першою
        const mainBranch = graph.branch(mainBranchData.name);
        branchMap[mainBranchData.name] = mainBranch;

        // Рендеримо коміти хронологічно
        const rendered = new Set();

        allCommits.forEach(c => {
            if (rendered.has(c.sha)) return;
            rendered.add(c.sha);

            const branchName = c.branch;
            if (!branchMap[branchName]) {
                branchMap[branchName] = branchMap[mainBranchData.name].branch(branchName);
            }

            const branch = branchMap[branchName];
            branch.commit({
                subject: c.message.length > 52 ? c.message.slice(0, 52) + '…' : c.message,
                hash: c.sha,
                style: {
                    dot: {
                        cursor: 'pointer',
                    },
                },
                onClick: (commit) => {
                    // Підсвічуємо вибраний коміт
                    window.gitGraphInterop._highlightCommit(containerId, commit.hash);
                    // Повідомляємо Blazor
                    dotNetRef.invokeMethodAsync('OnCommitClicked', commit.hash, commit.subject);
                },
            });
        });

        window.gitGraphInterop._instance = { graph, container };
    },

    // Підсвітити вибраний коміт (додаємо клас до SVG circle)
    _highlightCommit: function (containerId, sha) {
        const container = document.getElementById(containerId);
        if (!container) return;
        // Прибираємо попереднє підсвічування
        container.querySelectorAll('[data-selected]').forEach(el => {
            el.removeAttribute('data-selected');
            el.style.filter = '';
        });
        // Знаходимо коміт по title або data-hash
        const circles = container.querySelectorAll('circle');
        circles.forEach(circle => {
            const g = circle.closest('g[data-testid]') || circle.parentElement;
            if (g && g.textContent.includes(sha.slice(0, 7))) {
                circle.setAttribute('data-selected', 'true');
                circle.style.filter = 'drop-shadow(0 0 6px #92e0c6)';
            }
        });
    },

    // Позначити коміт як "assigned" (зеленим кольором)
    markAssigned: function (containerId, sha, assigned) {
        const container = document.getElementById(containerId);
        if (!container) return;
        const circles = container.querySelectorAll('circle');
        circles.forEach(circle => {
            const g = circle.closest('g') || circle.parentElement;
            if (g && g.textContent.includes(sha.slice(0, 7))) {
                circle.style.strokeWidth = assigned ? '3' : '2';
                circle.style.stroke = assigned ? '#92e0c6' : 'transparent';
            }
        });
    },

    destroy: function (containerId) {
        const container = document.getElementById(containerId);
        if (container) container.innerHTML = '';
        window.gitGraphInterop._instance = null;
    }
};
