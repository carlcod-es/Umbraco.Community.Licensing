import {
  LitElement,
  css,
  html,
  nothing,
  customElement,
  state,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { getDemoEntries, postDemoSelect } from "../api/index.js";
import type { DemoEntriesResponse } from "../api/index.js";

const CONFIGURED_KEY = "Configured key";
// Same literal the licensing dashboard listens for; nothing is imported from it.
const LICENSE_CHANGED_EVENT = "acme-contact-form-license-changed";

@customElement("acme-contact-form-demo-picker")
export class AcmeContactFormDemoPickerElement extends UmbElementMixin(LitElement) {
  @state() private _response?: DemoEntriesResponse;
  @state() private _message?: string;
  @state() private _busy = false;

  connectedCallback() {
    super.connectedCallback();
    this.#load();
  }

  async #load() {
    // Anything but 200 (404 demo off, 401/403 not allowed) means no picker, and no text.
    const result = await getDemoEntries({ throwOnError: false });
    this._response = result.response?.status === 200 ? result.data : undefined;
  }

  async #select(entry: string) {
    if (this._busy || entry === this._response?.selected) return;
    this._busy = true;
    this._message = undefined;
    try {
      const result = await postDemoSelect({ body: { entry }, throwOnError: false });
      const status = result.response?.status;
      if (status === 204) {
        await this.#load();
        window.dispatchEvent(new Event(LICENSE_CHANGED_EVENT));
      } else if (status === 400) {
        const problem = result.error as { detail?: string; title?: string } | undefined;
        this._message = problem?.detail ?? problem?.title ?? "That entry could not be selected.";
      } else {
        this._message = "The selection could not be changed.";
      }
    } catch {
      this._message = "The selection could not be changed.";
    } finally {
      this._busy = false;
    }
  }

  render() {
    const r = this._response;
    if (!r) return nothing;
    const current = r.entries.find((e) => e.name === r.selected);
    return html`
      <uui-box headline="Demo key">
        ${r.selected !== CONFIGURED_KEY
          ? html`<p>The dashboard shows the demo selection "${r.selected}".</p>`
          : nothing}
        ${current?.hasDemoDate && current.demoDate
          ? html`<p>Demo date: ${current.demoDate}</p>`
          : nothing}
        <ul>
          ${r.entries.map(
            (e) => html`<li>
              <uui-button
                look=${e.name === r.selected ? "primary" : "secondary"}
                label=${e.name}
                ?disabled=${this._busy}
                aria-pressed=${e.name === r.selected ? "true" : "false"}
                @click=${() => this.#select(e.name)}></uui-button>
              <span>${e.description}</span>
            </li>`,
          )}
        </ul>
        ${this._message ? html`<p class="error" role="alert">${this._message}</p>` : nothing}
      </uui-box>
    `;
  }

  static override styles = [
    css`
      :host {
        display: block;
      }
      ul {
        list-style: none;
        margin: 0;
        padding: 0;
      }
      li {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-4);
        margin-bottom: var(--uui-size-space-2);
      }
      .error {
        color: var(--uui-color-danger);
      }
    `,
  ];
}

export default AcmeContactFormDemoPickerElement;

declare global {
  interface HTMLElementTagNameMap {
    "acme-contact-form-demo-picker": AcmeContactFormDemoPickerElement;
  }
}
