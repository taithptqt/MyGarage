import { SECTION_META } from "./inventoryAdminHelpers";

export function SectionHeader({ section }) {
  const meta = SECTION_META[section] || {
    title: "Quản trị kho",
    subtitle: "Theo dõi và cấu hình nghiệp vụ kho",
    hint: "Chọn menu bên trái để thao tác."
  };
  return (
    <header className="admin-header">
      <div>
        <h1>{meta.title}</h1>
        <p className="header-sub">{meta.subtitle}</p>
        <div className="inventory-header-tip">{meta.hint}</div>
      </div>
    </header>
  );
}

export function StatCards({ items }) {
  return (
    <div className="stats-bar">
      {items.map((item) => (
        <div className="stat-card" key={item.label}>
          <div className="stat-value">{item.value}</div>
          <div className="stat-label">{item.label}</div>
        </div>
      ))}
    </div>
  );
}

export function ActionPanel({ title, description, children }) {
  return (
    <section className="inventory-panel">
      <div className="inventory-panel-head">
        <h3>{title}</h3>
        {description ? <p>{description}</p> : null}
      </div>
      <div className="inventory-panel-body">{children}</div>
    </section>
  );
}

export function NoDataRow({ colSpan, message }) {
  return (
    <tr>
      <td colSpan={colSpan}>
        <div className="inventory-empty">{message}</div>
      </td>
    </tr>
  );
}
