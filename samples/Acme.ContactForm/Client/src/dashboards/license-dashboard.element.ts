import {
  LitElement,
  css,
  html,
  nothing,
  customElement,
  state,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { getLicenseStatus } from "../api/index.js";
import type { AdminLicenseStatus } from "../api/index.js";

/**
 * Extension points for add-ons (the demo picker uses both, with no edits here):
 * - Dispatch this event on `window` after changing the configuration; the dashboard re-fetches.
 * - Register an extension of this type; it renders in the slot at the foot of the dashboard.
 */
const LICENSE_CHANGED_EVENT = "acme-contact-form-license-changed";
const LICENSE_PANEL_EXTENSION_TYPE = "acmeContactFormLicensePanel";

@customElement("acme-contact-form-license-dashboard")
export class AcmeContactFormLicenseDashboardElement extends UmbElementMixin(LitElement) {
  @state() private _status?: AdminLicenseStatus;
  @state() private _failed = false;
  @state() private _loading = true;

  connectedCallback() {
    super.connectedCallback();
    window.addEventListener(LICENSE_CHANGED_EVENT, this.#load);
    this.#load();
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    window.removeEventListener(LICENSE_CHANGED_EVENT, this.#load);
  }

  #load = async () => {
    this._loading = true;
    const { data, error } = await tryExecute(this, getLicenseStatus());
    this._failed = !!error || !data;
    this._status = this._failed ? undefined : data;
    this._loading = false;
  };

  #row(label: string, value: unknown) {
    return html`<dt>${label}</dt><dd>${value}</dd>`;
  }

  #renderStatus(s: AdminLicenseStatus) {
    return html`
      <uui-box headline="Contact form license">
        <dl>
          ${this.#row("State", html`<strong>${s.state}</strong>`)}
          ${s.firstAction ? this.#row("First action", s.firstAction) : nothing}
          ${s.keyIdentifier
            ? this.#row(
                "Key identifier",
                html`${s.keyIdentifier}
                ${s.keyIdentifierVerified
                  ? nothing
                  : html`<uui-tag look="secondary" color="warning">unverified</uui-tag>`}`,
              )
            : nothing}
          ${s.claimedProduct ? this.#row("Claimed product", s.claimedProduct) : nothing}
          ${s.expiry ? this.#row("Expiry", s.expiry) : nothing}
          ${s.expiringSoon && s.expiringSoonText
            ? this.#row("Expiring soon", html`<uui-tag color="warning" look="primary">${s.expiringSoonText}</uui-tag>`)
            : nothing}
          ${this.#row("Usage this month", s.usage.text)}
          ${this.#row(
            "Domain",
            html`${s.domain.text}
            ${s.domain.checked
              ? html`<uui-tag color=${s.domain.covered ? "positive" : "danger"} look="primary">
                  ${s.domain.covered ? "covered" : "not covered"}
                </uui-tag>`
              : nothing}`,
          )}
          ${this.#row("Setting", html`<code>${s.settingName}</code>`)}
          ${this.#row("Environment variable", html`<code>${s.environmentVariable}</code>`)}
          ${this.#row("Key source", s.keySourceName)}
        </dl>
      </uui-box>
      <uui-box headline="Features">
        <ul>
          ${s.features.map(
            (f) => html`<li class=${f.inEffect ? "in-effect" : f.included ? "lapsed" : "absent"}>
              <strong>${f.name}</strong>: ${f.grants}
            </li>`,
          )}
        </ul>
      </uui-box>
    `;
  }

  render() {
    return html`
      ${this._failed
        ? html`<uui-box><p class="error">The license status could not be loaded. Reload the page to try again.</p></uui-box>`
        : this._status
          ? this.#renderStatus(this._status)
          : this._loading
            ? html`<uui-loader></uui-loader>`
            : nothing}
      <umb-extension-slot type=${LICENSE_PANEL_EXTENSION_TYPE}></umb-extension-slot>
    `;
  }

  static override styles = [
    css`
      :host {
        display: block;
        padding: var(--uui-size-layout-1);
      }
      uui-box {
        margin-bottom: var(--uui-size-layout-1);
      }
      dl {
        display: grid;
        grid-template-columns: max-content 1fr;
        gap: var(--uui-size-space-3) var(--uui-size-space-5);
        margin: 0;
      }
      dt {
        font-weight: bold;
      }
      dd {
        margin: 0;
      }
      ul {
        margin: 0;
        padding-left: var(--uui-size-space-5);
      }
      .lapsed {
        color: var(--uui-color-danger);
      }
      .absent {
        color: var(--uui-color-disabled-contrast);
      }
      .error {
        color: var(--uui-color-danger);
      }
    `,
  ];
}

export default AcmeContactFormLicenseDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    "acme-contact-form-license-dashboard": AcmeContactFormLicenseDashboardElement;
  }
}
