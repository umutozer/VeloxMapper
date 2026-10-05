import type { Metadata } from "next";
import Link from "next/link";
import {
  Activity,
  ArrowRight,
  Binary,
  Bot,
  Check,
  Code2,
  Cpu,
  FileText,
  Info,
  Layers,
  Scale,
  ShieldCheck,
} from "lucide-react";
import CodeSample from "@/components/CodeSample";
import CodeTabs from "@/components/docs/CodeTabs";
import InstallCommand from "@/components/landing/InstallCommand";
import { APP_VERSION } from "@/lib/version";
import { SITE_DESCRIPTION } from "@/lib/site";
import styles from "./page.module.css";

export const metadata: Metadata = {
  title: "VeloxMapper — AutoMapper uyumlu, MIT lisanslı .NET nesne eşleme",
  description: SITE_DESCRIPTION,
  alternates: { canonical: "/" },
};

// ---------------------------------------------------------------------------
// Kod örnekleri — API: Profile, CreateMap, ForMember, MapFrom, IMapper,
// AddVeloxMapper, ProjectTo (VeloxMapper.QueryableExtensions)
// ---------------------------------------------------------------------------

const PROFILE_CODE = `using VeloxMapper;

public class OrderProfile : Profile
{
    public OrderProfile()
    {
        CreateMap<Order, OrderDto>()
            .ForMember(d => d.CustomerName,
                o => o.MapFrom(s => s.Customer.Name));
    }
}`;

const PROGRAM_CODE = `using VeloxMapper;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddVeloxMapper(typeof(Program));

var app = builder.Build();`;

const SERVICE_CODE = `using VeloxMapper;

public class OrderService(IMapper mapper)
{
    public OrderDto Get(Order order) =>
        mapper.Map<OrderDto>(order);
}`;

const QUERY_CODE = `using Microsoft.EntityFrameworkCore;
using VeloxMapper;
using VeloxMapper.QueryableExtensions;

public class OrderQueries(AppDbContext db, IMapper mapper)
{
    public Task<List<OrderDto>> ListAsync() =>
        db.Orders
            .ProjectTo<OrderDto>(mapper.ConfigurationProvider)
            .ToListAsync();
}`;

const compare = (ns: string, register: string) => `using ${ns};

public class OrderProfile : Profile
{
    public OrderProfile()
    {
        CreateMap<Order, OrderDto>()
            .ForMember(d => d.CustomerName,
                o => o.MapFrom(s => s.Customer.Name));
    }
}

// Program.cs
builder.Services.${register}(typeof(Program));`;

const BEFORE_CODE = compare("AutoMapper", "AddAutoMapper");
const AFTER_CODE = compare("VeloxMapper", "AddVeloxMapper");

// ---------------------------------------------------------------------------
// İçerik
// ---------------------------------------------------------------------------

const API_NAMES = ["Profile", "CreateMap", "ForMember", "MapFrom", "IMapper", "MapperConfiguration", "ProjectTo", "AssertConfigurationIsValid"];

const FEATURES = [
  {
    icon: Code2,
    title: "Aynı API, aynı isimler",
    desc: "Profile, CreateMap, ForMember, MapFrom, IMapper, MapperConfiguration, ProjectTo ve AssertConfigurationIsValid — AutoMapper'dan bildiğiniz isim ve kullanım biçimleriyle.",
  },
  {
    icon: Scale,
    title: "MIT lisansı",
    desc: "Ticari projelerde ücretsiz kullanın. Lisans anahtarı, kullanıcı sayısı ya da gelir eşiği yok; kaynak kodu GitHub'da açık.",
  },
  {
    icon: Layers,
    title: "Hibrit eşleme motoru",
    desc: "Derleme zamanında Source Generator ile reflection içermeyen kod; kapsanmayan senaryolarda çalışma zamanı Expression Tree derlemesi.",
  },
  {
    icon: Cpu,
    title: "NativeAOT dostu",
    desc: "Source Generator yolu, trimming ve NativeAOT yayınlarıyla uyumlu statik eşleme kodu üretir.",
  },
  {
    icon: ShieldCheck,
    title: "Fail-fast doğrulama",
    desc: "AssertConfigurationIsValid, eşlenmemiş ya da hatalı yapılandırılmış üyeleri uygulama başlarken raporlar — hatalar production'a ulaşmadan.",
  },
  {
    icon: Activity,
    title: "Gözlemlenebilirlik",
    desc: "Mapping planı raporları ve teşhis araçlarıyla her hedef üyenin hangi kurala göre eşlendiğini görün ve inceleyin.",
  },
];

const STEPS = [
  {
    n: "01",
    title: "Paketi değiştirin",
    desc: "AutoMapper paketini kaldırıp VeloxMapper'ı ekleyin. .NET 8, 9 ve 10 desteklenir.",
    code: "dotnet remove package AutoMapper\ndotnet add package VeloxMapper",
    link: { label: "Kurulum", href: "/docs/installation" },
  },
  {
    n: "02",
    title: "using'leri güncelleyin",
    desc: "using AutoMapper; satırlarını using VeloxMapper; olarak değiştirin — elle ya da otomatik geçiş betiğiyle.",
    code: "- using AutoMapper;\n+ using VeloxMapper;",
    link: { label: "Otomatik geçiş betiği", href: "/docs/migration-script" },
  },
  {
    n: "03",
    title: "Doğrulayın",
    desc: "Yapılandırmanızı başlangıçta doğrulayın; eksik ya da farklı davranan bir eşleme varsa ilk çalıştırmada görün.",
    code: "mapper.ConfigurationProvider\n      .AssertConfigurationIsValid();",
    link: { label: "Yapılandırma doğrulama", href: "/docs/configuration-validation" },
  },
];

const COMPAT_ROWS: { auto: string; velox: string; status: "same" | "alias" | "change"; note?: string }[] = [
  { auto: "using AutoMapper;", velox: "using VeloxMapper;", status: "change" },
  { auto: "services.AddAutoMapper(...)", velox: "services.AddVeloxMapper(...)", status: "alias", note: "AddAutoMapper uyumluluk takma adı olarak da çalışır." },
  { auto: "Profile", velox: "Profile", status: "same" },
  { auto: "CreateMap<TSource, TDestination>()", velox: "CreateMap<TSource, TDestination>()", status: "same" },
  { auto: "ForMember(…, o => o.MapFrom(…))", velox: "ForMember(…, o => o.MapFrom(…))", status: "same" },
  { auto: "IMapper.Map<TDestination>(source)", velox: "IMapper.Map<TDestination>(source)", status: "same" },
  { auto: "MapperConfiguration", velox: "MapperConfiguration", status: "same" },
  { auto: "ProjectTo<TDestination>(…)", velox: "ProjectTo<TDestination>(…)", status: "same", note: "Ad alanı: VeloxMapper.QueryableExtensions" },
  { auto: "AssertConfigurationIsValid()", velox: "AssertConfigurationIsValid()", status: "same" },
];

const STATUS_LABEL = { same: "Aynı", alias: "Takma ad", change: "Değişir" } as const;

const ECOSYSTEM = [".NET 8 · 9 · 10", "ASP.NET Core", "Minimal API", "Microsoft.Extensions.DependencyInjection", "Entity Framework Core (ProjectTo)", "NativeAOT & trimming"];

const FAQ = [
  {
    q: "VeloxMapper ticari projelerde gerçekten ücretsiz mi?",
    a: "Evet. VeloxMapper MIT lisansıyla dağıtılır: ticari kullanım, değiştirme ve yeniden dağıtım serbesttir. Lisans anahtarı ya da kayıt gerekmez.",
  },
  {
    q: "Geçiş için kodumda ne kadar değişiklik gerekir?",
    a: "Çoğu projede NuGet paketini değiştirmek ve using AutoMapper; satırlarını using VeloxMapper; yapmak yeterlidir. Profiller, CreateMap/ForMember çağrıları ve IMapper kullanımı aynı kalır.",
  },
  {
    q: "AutoMapper ile birebir aynı mı davranıyor?",
    a: "Hedef, yaygın kullanım senaryolarında aynı davranıştır. Bilinen farklar Davranış Farkları sayfasında belgelenir; AssertConfigurationIsValid ile yapılandırmanızı başlangıçta doğrulamanızı öneririz.",
  },
  {
    q: "Geçişi bir yapay zekâ asistanıyla yapabilir miyim?",
    a: "Evet. Dokümantasyonun tamamı llms.txt ve llms-full.txt olarak sunulur; her sayfa Markdown olarak kopyalanabilir. Hazır yönergeler için Yapay Zekâ ile Geçiş sayfasına bakın.",
  },
];

export default function Home() {
  return (
    <main id="main-content" className={styles.main}>
      {/* ------------------------------------------------------------ HERO */}
      <section className={styles.hero}>
        <div className={styles.heroGrid}>
          <div className={styles.heroCopy}>
            <Link href="/docs/changelog" className={styles.announce}>
              <span className={styles.announceTag}>v{APP_VERSION}</span>
              <span>MIT lisanslı · AutoMapper ile API uyumlu</span>
              <ArrowRight size={14} aria-hidden="true" />
            </Link>

            <h1 className={styles.heroTitle}>
              AutoMapper&apos;dan <span className={styles.heroAccent}>tek adımda</span> geçiş.
            </h1>

            <p className={styles.heroLead}>
              VeloxMapper, AutoMapper ile aynı API&apos;yi konuşan açık kaynak bir .NET nesne eşleme
              kütüphanesidir. Paketi değiştirin, <code>using</code> satırlarını güncelleyin — profilleriniz,
              eşleme kurallarınız ve <code>IMapper</code> çağrılarınız olduğu gibi çalışmaya devam etsin.
            </p>

            <InstallCommand />

            <div className={styles.ctaRow}>
              <Link href="/docs/migration-guide" className="btn btn--primary">
                Geçiş rehberi
                <ArrowRight size={16} aria-hidden="true" />
              </Link>
              <Link href="/docs/quickstart" className="btn btn--secondary">
                Hızlı başlangıç
              </Link>
            </div>

            <ul className={styles.heroMeta}>
              <li>
                <Check size={15} aria-hidden="true" /> MIT lisansı, lisans anahtarı yok
              </li>
              <li>
                <Check size={15} aria-hidden="true" /> Ticari kullanım ücretsiz
              </li>
              <li>
                <Check size={15} aria-hidden="true" /> .NET 8 · 9 · 10
              </li>
            </ul>
          </div>

          <div className={styles.heroVisual}>
            <div className={styles.window}>
              <CodeTabs tabs={["OrderProfile.cs", "Program.cs", "OrderService.cs", "OrderQueries.cs"]}>
                <CodeSample code={PROFILE_CODE} lang="csharp" inTabs />
                <CodeSample code={PROGRAM_CODE} lang="csharp" inTabs highlight="5" />
                <CodeSample code={SERVICE_CODE} lang="csharp" inTabs />
                <CodeSample code={QUERY_CODE} lang="csharp" inTabs highlight="9" />
              </CodeTabs>
            </div>
          </div>
        </div>
      </section>

      {/* ------------------------------------------------------------ ÖNCE / SONRA */}
      <section className={styles.section} aria-labelledby="compare-title">
        <div className={styles.sectionHead}>
          <p className={styles.eyebrow}>Geçiş</p>
          <h2 id="compare-title" className={styles.sectionTitle}>
            Kodunuz aynı kalır. Değişen yalnızca birkaç satır.
          </h2>
          <p className={styles.sectionLead}>
            Profil sınıfları, <code>CreateMap</code> / <code>ForMember</code> zincirleri ve <code>MapFrom</code>{" "}
            ifadeleri birebir aynıdır. Değişen; paket referansı, <code>using</code> satırı ve isterseniz DI kaydı.
          </p>
        </div>

        <div className={styles.packageSwap}>
          <code className={styles.swapRemove}>dotnet remove package AutoMapper</code>
          <ArrowRight size={16} aria-hidden="true" className={styles.swapArrow} />
          <code className={styles.swapAdd}>dotnet add package VeloxMapper</code>
        </div>

        <div className={styles.compareGrid}>
          <div className={styles.compareCol}>
            <p className={styles.compareLabel}>
              <span className={styles.dotMuted} aria-hidden="true" /> Önce — AutoMapper
            </p>
            <CodeSample code={BEFORE_CODE} lang="csharp" title="OrderProfile.cs" highlight="1,14" />
          </div>
          <div className={styles.compareCol}>
            <p className={styles.compareLabel}>
              <span className={styles.dotAccent} aria-hidden="true" /> Sonra — VeloxMapper
            </p>
            <CodeSample code={AFTER_CODE} lang="csharp" title="OrderProfile.cs" highlight="1,14" />
          </div>
        </div>

        <p className={styles.compareNote}>
          <Info size={16} aria-hidden="true" />
          <span>
            <code>AddAutoMapper</code> uyumluluk takma adı olarak da çalışır — dilerseniz kayıt satırına hiç
            dokunmadan geçebilir, <code>AddVeloxMapper</code>&apos;a daha sonra geçebilirsiniz.
          </span>
        </p>
      </section>

      {/* ------------------------------------------------------------ ÖZELLİKLER */}
      <section className={styles.section} aria-labelledby="features-title">
        <div className={styles.sectionHead}>
          <p className={styles.eyebrow}>Neden VeloxMapper</p>
          <h2 id="features-title" className={styles.sectionTitle}>
            Tanıdık API. Açık lisans. Modern .NET için bir motor.
          </h2>
        </div>
        <ul className={styles.featureGrid}>
          {FEATURES.map((f) => {
            const Icon = f.icon;
            return (
              <li key={f.title} className={styles.featureCard}>
                <span className={styles.featureIcon} aria-hidden="true">
                  <Icon size={19} />
                </span>
                <h3 className={styles.featureTitle}>{f.title}</h3>
                <p className={styles.featureDesc}>{f.desc}</p>
              </li>
            );
          })}
        </ul>
      </section>

      {/* ------------------------------------------------------------ 3 ADIM */}
      <section className={styles.band} aria-labelledby="steps-title">
        <div className={styles.bandInner}>
          <div className={styles.sectionHead}>
            <p className={styles.eyebrow}>3 adımda geçiş</p>
            <h2 id="steps-title" className={styles.sectionTitle}>
              Üç adımda, öngörülebilir bir geçiş.
            </h2>
          </div>
          <ol className={styles.steps}>
            {STEPS.map((s) => (
              <li key={s.n} className={styles.step}>
                <span className={styles.stepNo}>{s.n}</span>
                <h3 className={styles.stepTitle}>{s.title}</h3>
                <p className={styles.stepDesc}>{s.desc}</p>
                <pre className={styles.stepCode}>
                  <code>{s.code}</code>
                </pre>
                <Link href={s.link.href} className={styles.stepLink}>
                  {s.link.label}
                  <ArrowRight size={14} aria-hidden="true" />
                </Link>
              </li>
            ))}
          </ol>
        </div>
      </section>

      {/* ------------------------------------------------------------ MİMARİ */}
      <section className={styles.section} aria-labelledby="engine-title">
        <div className={styles.sectionHead}>
          <p className={styles.eyebrow}>Mimari</p>
          <h2 id="engine-title" className={styles.sectionTitle}>
            İki katmanlı motor, tek bir API.
          </h2>
          <p className={styles.sectionLead}>
            Eşlemeleriniz aynı <code>Profile</code> sınıflarında tanımlanır; VeloxMapper her eşleme için uygun
            katmanı kendisi seçer.
          </p>
        </div>

        <div className={styles.engine}>
          <div className={styles.engineInput}>
            <span className={styles.engineCaption}>Yapılandırmanız</span>
            <code>CreateMap&lt;Order, OrderDto&gt;()</code>
          </div>
          <div className={styles.engineLanes}>
            <div className={styles.lane}>
              <div className={styles.laneHead}>
                <span className={styles.laneIcon} aria-hidden="true">
                  <Binary size={18} />
                </span>
                <div>
                  <p className={styles.laneTag}>Derleme zamanı</p>
                  <h3 className={styles.laneTitle}>Source Generator</h3>
                </div>
              </div>
              <ul className={styles.laneList}>
                <li>
                  <Check size={14} aria-hidden="true" /> Reflection içermeyen, derleme anında üretilen eşleme kodu
                </li>
                <li>
                  <Check size={14} aria-hidden="true" /> NativeAOT ve trimming ile uyumlu
                </li>
                <li>
                  <Check size={14} aria-hidden="true" /> Üretilen kod IDE&apos;de incelenebilir
                </li>
              </ul>
            </div>
            <div className={styles.lane}>
              <div className={styles.laneHead}>
                <span className={styles.laneIcon} aria-hidden="true">
                  <Layers size={18} />
                </span>
                <div>
                  <p className={styles.laneTag}>Çalışma zamanı</p>
                  <h3 className={styles.laneTitle}>Expression Tree derlemesi</h3>
                </div>
              </div>
              <ul className={styles.laneList}>
                <li>
                  <Check size={14} aria-hidden="true" /> Source Generator&apos;ın kapsamadığı senaryolarda devreye girer
                </li>
                <li>
                  <Check size={14} aria-hidden="true" /> Derlenen eşleme delegeleri önbelleğe alınır
                </li>
                <li>
                  <Check size={14} aria-hidden="true" /> Ek yapılandırma gerektirmez
                </li>
              </ul>
            </div>
          </div>
          <div className={styles.engineOutput}>
            <ShieldCheck size={16} aria-hidden="true" />
            <span>Başlangıçta doğrulanan, raporlanabilir eşleme planı</span>
          </div>
        </div>
        <Link href="/docs/performance" className={styles.inlineLink}>
          Performans &amp; Source Generator
          <ArrowRight size={14} aria-hidden="true" />
        </Link>
      </section>

      {/* ------------------------------------------------------------ UYUMLULUK */}
      <section className={styles.section} aria-labelledby="compat-title">
        <div className={styles.sectionHead}>
          <p className={styles.eyebrow}>Uyumluluk</p>
          <h2 id="compat-title" className={styles.sectionTitle}>
            Ekibinizin zaten bildiği API.
          </h2>
          <p className={styles.sectionLead}>
            En sık kullanılan AutoMapper API&apos;lerinin VeloxMapper karşılıkları. Tam liste ve bilinen davranış
            farkları dokümantasyonda.
          </p>
        </div>

        <div className={styles.compatLayout}>
          <div className={styles.tableWrap} role="region" aria-label="AutoMapper – VeloxMapper API karşılaştırması" tabIndex={0}>
            <table className={styles.table}>
              <thead>
                <tr>
                  <th scope="col">AutoMapper</th>
                  <th scope="col">VeloxMapper</th>
                  <th scope="col">Durum</th>
                </tr>
              </thead>
              <tbody>
                {COMPAT_ROWS.map((r) => (
                  <tr key={r.auto}>
                    <td>
                      <code>{r.auto}</code>
                    </td>
                    <td>
                      <code>{r.velox}</code>
                      {r.note && <span className={styles.rowNote}>{r.note}</span>}
                    </td>
                    <td>
                      <span className={styles.status} data-status={r.status}>
                        {STATUS_LABEL[r.status]}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <aside className={styles.compatAside}>
            <h3 className={styles.asideTitle}>Ekosistem</h3>
            <ul className={styles.ecoList}>
              {ECOSYSTEM.map((e) => (
                <li key={e}>
                  <Check size={14} aria-hidden="true" />
                  {e}
                </li>
              ))}
            </ul>
            <div className={styles.asideLinks}>
              <Link href="/docs/api-mapping" className={styles.inlineLink}>
                API eşleme tablosu <ArrowRight size={14} aria-hidden="true" />
              </Link>
              <Link href="/docs/behavior-differences" className={styles.inlineLink}>
                Davranış farkları <ArrowRight size={14} aria-hidden="true" />
              </Link>
            </div>
          </aside>
        </div>

        <p className={styles.apiChips} aria-label="Aynı kalan API isimleri">
          {API_NAMES.map((n) => (
            <code key={n}>{n}</code>
          ))}
        </p>
      </section>

      {/* ------------------------------------------------------------ YAPAY ZEKÂ */}
      <section className={styles.aiBand} aria-labelledby="ai-title">
        <div className={styles.aiCopy}>
          <span className={styles.aiIcon} aria-hidden="true">
            <Bot size={20} />
          </span>
          <div>
            <h2 id="ai-title" className={styles.aiTitle}>
              Yapay zekâ asistanınızla geçiş yapın
            </h2>
            <p className={styles.aiDesc}>
              Dokümantasyonun tamamı makine tarafından okunabilir biçimde yayımlanır. Asistanınıza bağlamı verin,
              geçişi birlikte yapın.
            </p>
          </div>
        </div>
        <ul className={styles.aiLinks}>
          <li>
            <a href="/llms.txt" target="_blank" rel="noopener">
              <FileText size={15} aria-hidden="true" />
              <code>llms.txt</code>
              <span>Sayfa dizini</span>
            </a>
          </li>
          <li>
            <a href="/llms-full.txt" target="_blank" rel="noopener">
              <FileText size={15} aria-hidden="true" />
              <code>llms-full.txt</code>
              <span>Tüm dokümantasyon</span>
            </a>
          </li>
          <li>
            <Link href="/docs/migration-prompt">
              <Bot size={15} aria-hidden="true" />
              <strong>Yapay Zekâ ile Geçiş</strong>
              <span>Hazır yönergeler</span>
            </Link>
          </li>
        </ul>
      </section>

      {/* ------------------------------------------------------------ SSS */}
      <section className={styles.section} aria-labelledby="faq-title">
        <div className={styles.faqLayout}>
          <div className={styles.sectionHead}>
            <p className={styles.eyebrow}>SSS</p>
            <h2 id="faq-title" className={styles.sectionTitle}>
              Sık sorulan sorular
            </h2>
            <p className={styles.sectionLead}>Geçiş kararı vermeden önce en çok sorulanlar.</p>
            <Link href="/docs/faq" className={styles.inlineLink}>
              Tüm sorular <ArrowRight size={14} aria-hidden="true" />
            </Link>
          </div>
          <div className={styles.faqList}>
            {FAQ.map((f) => (
              <details key={f.q} className={styles.faqItem}>
                <summary>{f.q}</summary>
                <p>{f.a}</p>
              </details>
            ))}
          </div>
        </div>
      </section>

      {/* ------------------------------------------------------------ CTA */}
      <section className={styles.finalCta} aria-labelledby="cta-title">
        <h2 id="cta-title" className={styles.ctaTitle}>
          Geçişe bugün başlayın.
        </h2>
        <p className={styles.ctaLead}>
          Paketi ekleyin, geçiş rehberini izleyin ve yapılandırmanızı <code>AssertConfigurationIsValid</code> ile
          doğrulayın.
        </p>
        <InstallCommand />
        <div className={styles.ctaRow}>
          <Link href="/docs/migration-guide" className="btn btn--primary">
            Geçiş rehberi
            <ArrowRight size={16} aria-hidden="true" />
          </Link>
          <Link href="/docs/quickstart" className="btn btn--secondary">
            Hızlı başlangıç
          </Link>
        </div>
      </section>
    </main>
  );
}
