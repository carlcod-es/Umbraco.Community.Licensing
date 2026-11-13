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
import { getSubmissions } from "../api/index.js";
import type { SubmissionsResponse } from "../api/index.js";

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

@customElement("acme-contact-form-submissions-dashboard")
export class AcmeContactFormSubmissionsDashboardElement extends UmbElementMixin(LitElement) {
  @state() private _response?: SubmissionsResponse;
  @state() private _failed = false;
  @state() private _loading = true;

  connectedCallback() {
    super.connectedCallback();
    this.#load();
  }

  async #load() {
    this._loading = true;
    const { data, error } = await tryExecute(this, getSubmissions());
    this._failed = !!error || !data;
    this._response = this._failed ? undefined : data;
    this._loading = false;
  }

  #renderBanner() {
    const banner = this._response?.banner;
    return banner
      ? html`<uui-box><p class="banner" role="alert">${banner.message}</p></uui-box>`
      : nothing;
  }

  #renderTable(r: SubmissionsResponse) {
    if (r.submissions.length === 0) {
      return html`<uui-box headline="This month"><p>No submissions this month.</p></uui-box>`;
    }
    return html`
      <uui-box headline="This month">
        <uui-table>
          <uui-table-head>
            <uui-table-head-cell>Name</uui-table-head-cell>
            <uui-table-head-cell>Email</uui-table-head-cell>
            <uui-table-head-cell>Message</uui-table-head-cell>
            <uui-table-head-cell>Upload</uui-table-head-cell>
            <uui-table-head-cell>Received</uui-table-head-cell>
          </uui-table-head>
          ${r.submissions.map(
            (s) => html`<uui-table-row>
              <uui-table-cell>${s.name}</uui-table-cell>
              <uui-table-cell>${s.email}</uui-table-cell>
              <uui-table-cell>${s.message}</uui-table-cell>
              <uui-table-cell>
                ${s.uploadFileName
                  ? `${s.uploadFileName}${s.uploadSizeBytes != null ? ` (${formatSize(s.uploadSizeBytes)})` : ""}`
                  : ""}
              </uui-table-cell>
              <uui-table-cell>${new Date(s.receivedUtc).toLocaleString()}</uui-table-cell>
            </uui-table-row>`,
          )}
        </uui-table>
      </uui-box>
    `;
  }

  render() {
    if (this._failed) {
      return html`<uui-box><p class="error">The submissions could not be loaded. Reload the page to try again.</p></uui-box>`;
    }
    if (!this._response) {
      return this._loading ? html`<uui-loader></uui-loader>` : nothing;
    }
    return html`${this.#renderBanner()}${this.#renderTable(this._response)}`;
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
      .banner {
        margin: 0;
        padding: var(--uui-size-space-4);
        background: var(--uui-color-warning);
        color: var(--uui-color-warning-contrast);
        border-radius: var(--uui-border-radius);
      }
      .error {
        color: var(--uui-color-danger);
      }
    `,
  ];
}

export default AcmeContactFormSubmissionsDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    "acme-contact-form-submissions-dashboard": AcmeContactFormSubmissionsDashboardElement;
  }
}
