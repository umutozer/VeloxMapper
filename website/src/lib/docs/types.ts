/** İstemci ve sunucu tarafında paylaşılan dokümantasyon tipleri. */
export interface NavItem {
  slug: string;
  title: string;
  badge?: string;
}

export interface NavSection {
  id: string;
  title: string;
  items: NavItem[];
}

export interface TocItem {
  id: string;
  text: string;
  depth: 2 | 3;
}
