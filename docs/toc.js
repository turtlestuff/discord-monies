let discriminatorCounter = 0;
function autoToc() {
    let mdContent = document.getElementById("mdContent");
    let headerLinks = mdContent.querySelectorAll("h2,h3");
    let headers = [];
    for (l of headerLinks){
        if (l.id === "")
            l.id = l.innerText.replaceAll(" ", "_");

        if (l.tagName === "H2"){
            headers.push([{id: l.id, name: l.innerText}]);
        }
        else if (l.tagName === "H3"){
            let h2 = headers.pop();
            if (h2[1] == undefined) h2[1] = [];
            h2[1].push({id: l.id, name: l.innerText});
            headers.push(h2);
        }
    }

    let ol = document.createElement("ol");
    for(h2 of headers) {
        let li2 = document.createElement("li");     
        let a2 = document.createElement("a");
        a2.innerText = h2[0].name
        a2.href = "#" + h2[0].id;
        li2.append(a2);
        if (Array.isArray(h2[1])) {
            let ol3 = document.createElement("ol");
            for (h3 of h2[1]) {
                let li3 = document.createElement("li");
                let a3 = document.createElement("a");
                a3.innerText = h3.name;
                a3.href = "#" + h3.id;
                li3.append(a3);
                ol3.append(li3);
            }
            li2.append(ol3);
        }
        ol.append(li2);
    }
    document.getElementById("toc").innerHTML = ""
    document.getElementById("toc").append(ol);
}
