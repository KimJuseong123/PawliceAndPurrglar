using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PawliceAndPurrglar.UI
{
    /// <summary>
    /// The lobby's "how do I play this" panel, one page at a time.
    ///
    /// Everything here is resolved and wired at runtime rather than serialized
    /// by the builder, and that is not a style choice — it is this project's two
    /// most expensive UI traps, both of which this panel would walk straight
    /// into:
    ///
    /// - a listener added with <c>onClick.AddListener</c> from an editor script
    ///   is non-persistent and disappears when the scene is saved, so the arrows
    ///   would work in the editor and do nothing in the build (`ISSUE-017`)
    /// - a <c>List</c> filled by an editor script empties the same way, so the
    ///   page count would read zero and paging would quietly do nothing
    ///   (`ISSUE-031`)
    ///
    /// So the builder's only contract is the object names, and this component
    /// reads them. Pages are taken in sibling order, which is deterministic —
    /// unlike <c>FindObjectsByType</c>, whose order is an accident of instance
    /// ids and has broken a regression here before (`ISSUE-041`).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyHowToPanel : MonoBehaviour
    {
        /// <summary>Names the builder must use. Read, never written.</summary>
        public const string PagesNodeName = "Pages";
        public const string PreviousButtonName = "PreviousPage";
        public const string NextButtonName = "NextPage";
        public const string PageLabelName = "PageLabel";
        public const string DotsNodeName = "PageDots";

        private readonly List<GameObject> pages = new();
        private readonly List<Image> dots = new();

        private Button previousButton;
        private Button nextButton;
        private TMP_Text pageLabel;
        private bool wired;
        private int index;

        /// <summary>
        /// Zero-based page currently on screen. Public for the layout capture
        /// and the tests, which need to step through without pressing anything.
        /// </summary>
        public int PageIndex => index;

        public int PageCount => pages.Count;

        /// <summary>
        /// Filled and lit, so a colour that reads on the cream panel.
        /// </summary>
        private static readonly Color ActiveDot = new(0.22f, 0.15f, 0.12f, 1f);
        private static readonly Color IdleDot = new(0.22f, 0.15f, 0.12f, 0.28f);

        /// <summary>
        /// Reads the hierarchy and puts the first page on screen, without
        /// touching the buttons.
        ///
        /// Public because two callers cannot use <see cref="OnEnable"/>: the
        /// builder, which runs in the editor where lifecycle callbacks do not
        /// fire without <c>[ExecuteAlways]</c>, and the tests, for the same
        /// reason. The builder calling this is what makes the saved prefab open
        /// on page one with the left arrow already gone, instead of every page
        /// stacked on top of each other — which is how the layout capture
        /// rendered it, and it reads as the panel being broken.
        ///
        /// Deliberately does not wire the buttons. A listener added here would
        /// be added from an editor script, and those do not survive the scene
        /// being saved (`ISSUE-017`).
        /// </summary>
        public void Rebind()
        {
            Resolve();
            Apply();
        }

        private void OnEnable()
        {
            Resolve();
            Wire();
            Apply();
        }

        private void OnDisable()
        {
            if (previousButton != null)
            {
                previousButton.onClick.RemoveListener(ShowPrevious);
            }

            if (nextButton != null)
            {
                nextButton.onClick.RemoveListener(ShowNext);
            }

            wired = false;
        }

        public void ShowPrevious() => Show(index - 1);

        public void ShowNext() => Show(index + 1);

        public void Show(int page)
        {
            if (pages.Count == 0)
            {
                return;
            }

            // Clamped rather than wrapped. The arrows disappear at the ends, so
            // wrapping would contradict what the player is being shown.
            index = Mathf.Clamp(page, 0, pages.Count - 1);
            Apply();
        }

        private void Resolve()
        {
            pages.Clear();
            Transform pageRoot = transform.Find(PagesNodeName);
            if (pageRoot != null)
            {
                for (int child = 0; child < pageRoot.childCount; child++)
                {
                    pages.Add(pageRoot.GetChild(child).gameObject);
                }
            }

            previousButton = FindButton(PreviousButtonName);
            nextButton = FindButton(NextButtonName);

            Transform label = transform.Find(PageLabelName);
            pageLabel = label != null ? label.GetComponent<TMP_Text>() : null;

            dots.Clear();
            Transform dotRoot = transform.Find(DotsNodeName);
            if (dotRoot != null)
            {
                for (int child = 0; child < dotRoot.childCount; child++)
                {
                    Image dot = dotRoot.GetChild(child).GetComponent<Image>();
                    if (dot != null)
                    {
                        dots.Add(dot);
                    }
                }
            }

            index = Mathf.Clamp(index, 0, Mathf.Max(0, pages.Count - 1));
        }

        private Button FindButton(string name)
        {
            Transform found = transform.Find(name);
            return found != null ? found.GetComponent<Button>() : null;
        }

        private void Wire()
        {
            if (wired)
            {
                return;
            }

            previousButton?.onClick.AddListener(ShowPrevious);
            nextButton?.onClick.AddListener(ShowNext);
            wired = true;
        }

        /// <summary>
        /// Puts the current page on screen and the arrows in the right state.
        ///
        /// The arrows are switched off entirely rather than greyed out. A
        /// disabled button still occupies its place and still reads as a control
        /// that ought to work, and the request was for them not to be there.
        /// </summary>
        private void Apply()
        {
            for (int page = 0; page < pages.Count; page++)
            {
                if (pages[page] != null)
                {
                    pages[page].SetActive(page == index);
                }
            }

            if (previousButton != null)
            {
                previousButton.gameObject.SetActive(index > 0);
            }

            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(index < pages.Count - 1);
            }

            if (pageLabel != null)
            {
                pageLabel.text = pages.Count > 0
                    ? $"{index + 1} / {pages.Count}"
                    : string.Empty;
            }

            for (int dot = 0; dot < dots.Count; dot++)
            {
                dots[dot].color = dot == index ? ActiveDot : IdleDot;
            }
        }
    }
}
