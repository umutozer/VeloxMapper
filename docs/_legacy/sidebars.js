// Docusaurus sidebar konfigürasyonu — VeloxMapper dokümantasyonu
// @ts-check

/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
  docs: [
    'intro',
    {
      type: 'category',
      label: 'Başlangıç',
      collapsed: false,
      items: ['getting-started', 'core-concepts'],
    },
    {
      type: 'category',
      label: 'API Referansı',
      items: [
        'api/mapper',
        'api/configuration',
        'api/mapping-expression',
        'api/member-options',
        'api/interfaces',
        'api/attributes',
        'api/diagnostics',
      ],
    },
    {
      type: 'category',
      label: 'Extension Metotları',
      items: ['extension-methods'],
    },
    {
      type: 'category',
      label: 'İleri Düzey',
      items: ['advanced', 'scenarios'],
    },
    {
      type: 'category',
      label: 'Rehberler',
      items: ['migration', 'troubleshooting', 'best-practices'],
    },
    'analysis-gaps',
  ],
};

module.exports = sidebars;
