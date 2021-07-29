async function loadData() {
    let results = await fetch("/doc.md", {

    });
    let mdData = await results.text();

    let converter = new showdown.Converter({
        headerLevelStart: 2
    });
    let htmlData = converter.makeHtml(mdData);

    document.getElementById("mdContent").innerHTML = htmlData;
    
}
