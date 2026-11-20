// The mobile menu.
(function () {
  document.documentElement.classList.add("js");
  var toggle = document.querySelector(".menu-toggle");
  var nav = document.getElementById("topnav");
  if (toggle && nav) {
    toggle.addEventListener("click", function () {
      var open = toggle.getAttribute("aria-expanded") === "true";
      toggle.setAttribute("aria-expanded", String(!open));
      nav.classList.toggle("open", !open);
    });
  }
})();
